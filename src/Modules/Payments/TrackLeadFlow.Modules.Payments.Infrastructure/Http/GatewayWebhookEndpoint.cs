using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using TrackLeadFlow.Infrastructure.Persistence;
using TrackLeadFlow.Modules.Payments.Infrastructure.Persistence;

namespace TrackLeadFlow.Modules.Payments.Infrastructure.Http;

public sealed record GatewayWebhookResult(int StatusCode);

public sealed record ChargePaid(Guid TenantId, Guid ChargeId, string? CtwaClid, decimal Amount, string Currency);

public delegate void ChargePaidHandler(NpgsqlTransaction transaction, ChargePaid paid);

public sealed class GatewayWebhookEndpoint
{
    private readonly NpgsqlConnectionFactory _factory;
    private readonly ChargePaidHandler _onPaid;
    private readonly string _secret;

    public GatewayWebhookEndpoint(NpgsqlConnectionFactory factory, ChargePaidHandler onPaid, string secret)
    {
        _factory = factory;
        _onPaid = onPaid;
        _secret = secret;
    }

    public GatewayWebhookResult Receive(string rawBody, string? signatureHeader)
    {
        if (!SignatureIsValid(rawBody, signatureHeader))
        {
            return new GatewayWebhookResult(401);
        }

        var payload = JsonSerializer.Deserialize<GatewayNotice>(rawBody);
        if (payload is null || payload.ChargeId == Guid.Empty || string.IsNullOrWhiteSpace(payload.EventId))
        {
            return new GatewayWebhookResult(400);
        }

        using var session = _factory.Begin();
        var payments = new PaymentsRepository(session.Transaction);
        if (payments.ReceiptExists("gateway", payload.EventId))
        {
            session.Commit();
            return new GatewayWebhookResult(200);
        }

        var charge = payments.FindCharge(payload.ChargeId);
        payments.InsertWebhookReceipt(Guid.CreateVersion7(), "gateway", payload.EventId, DateTimeOffset.UtcNow);
        if (charge is not null && charge.Status == "Pending")
        {
            payments.MarkPaid(payload.ChargeId, DateTimeOffset.UtcNow);
            _onPaid(session.Transaction, new ChargePaid(charge.TenantId, charge.Id, payload.CtwaClid, charge.Amount, charge.Currency));
        }

        session.Commit();
        return new GatewayWebhookResult(200);
    }

    public static bool WhatsAppBodyConfirmsPayment(string? body) => false;

    private bool SignatureIsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var provided = Encoding.UTF8.GetBytes(signatureHeader.Trim());
        var actual = Encoding.UTF8.GetBytes(expected);
        return provided.Length == actual.Length && CryptographicOperations.FixedTimeEquals(provided, actual);
    }

    private sealed record GatewayNotice(Guid ChargeId, string EventId, string? CtwaClid);
}
