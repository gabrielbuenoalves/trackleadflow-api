using System.Text.Json;
using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Identity.Contracts.Facades;
using TrackLeadFlow.Modules.WhatsApp.Application.Routing;
using TrackLeadFlow.Modules.WhatsApp.Domain;
using TrackLeadFlow.Modules.WhatsApp.Infrastructure.External.CloudApi;
using TrackLeadFlow.Modules.WhatsApp.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.WhatsApp.Infrastructure.Http;

public sealed record WebhookHttpResult(int StatusCode, string Body);

public sealed class CloudApiWebhookEndpoint
{
    private readonly NpgsqlConnectionFactory _factory;
    private readonly IOperatorPhones _operatorPhones;
    private readonly IChannelAppSecret _secrets;
    private readonly string _verifyToken;

    public CloudApiWebhookEndpoint(
        NpgsqlConnectionFactory factory,
        IOperatorPhones operatorPhones,
        IChannelAppSecret secrets,
        string verifyToken)
    {
        _factory = factory;
        _operatorPhones = operatorPhones;
        _secrets = secrets;
        _verifyToken = verifyToken;
    }

    public WebhookHttpResult Verify(string? mode, string? token, string? challenge)
    {
        if (mode == "subscribe" && token == _verifyToken && !string.IsNullOrEmpty(challenge))
        {
            return new WebhookHttpResult(200, challenge);
        }

        return new WebhookHttpResult(403, "");
    }

    public WebhookHttpResult Receive(string rawBody, string? signatureHeader)
    {
        CloudApiChange change;
        try
        {
            change = CloudApiPayload.Read(rawBody);
        }
        catch (JsonException)
        {
            return new WebhookHttpResult(401, "");
        }
        catch (InvalidOperationException)
        {
            return new WebhookHttpResult(401, "");
        }
        catch (KeyNotFoundException)
        {
            return new WebhookHttpResult(401, "");
        }

        var secret = _secrets.Find(change.PhoneNumberId);
        if (secret is null || !WebhookSignature.IsValid(secret, rawBody, signatureHeader))
        {
            return new WebhookHttpResult(401, "");
        }

        using var session = _factory.Begin();
        var repository = new WhatsAppRepository(session.Transaction);
        var channel = repository.FindActiveChannel(change.PhoneNumberId);
        if (channel is null)
        {
            return new WebhookHttpResult(401, "");
        }

        var route = new RouteInboundMessage(_operatorPhones);
        var now = DateTimeOffset.UtcNow;
        foreach (var message in change.Messages)
        {
            if (string.IsNullOrWhiteSpace(message.Id) || repository.ReceiptExists("cloud-api", message.Id))
            {
                continue;
            }

            repository.InsertWebhookReceipt(Guid.CreateVersion7(), "cloud-api", message.Id, now);
            var phone = PhoneNumber.Normalize(message.From);
            var conversationId = repository.OpenConversation(channel.Value.TenantId, phone, now);
            repository.InsertMessage(Guid.CreateVersion7(), channel.Value.TenantId, conversationId, "Inbound", message.Body, message.Id, now);
            var payload = JsonSerializer.Serialize(new { conversationId, messageId = message.Id });
            if (route.Route(channel.Value.TenantId, message.From) == InboundLane.Operator)
            {
                repository.InsertOutbox(Guid.CreateVersion7(), channel.Value.TenantId, "OperatorMessageReceived", payload, now);
                continue;
            }

            repository.InsertOutbox(Guid.CreateVersion7(), channel.Value.TenantId, "InboundMessageReceived", payload, now);
            if (!string.IsNullOrWhiteSpace(message.CtwaClid)
                && repository.SetCtwaClidIfEmpty(conversationId, message.CtwaClid, now))
            {
                var contact = JsonSerializer.Serialize(new { conversationId, ctwaClid = message.CtwaClid });
                repository.InsertOutbox(Guid.CreateVersion7(), channel.Value.TenantId, "ContactStarted", contact, now);
            }
        }

        session.Commit();
        return new WebhookHttpResult(200, "");
    }
}
