using System.Text.Json;

namespace TrackLeadFlow.Modules.WhatsApp.Infrastructure.External.CloudApi;

public sealed record CloudApiMessage(string Id, string From, string? Body, string? CtwaClid);

public sealed record CloudApiChange(string PhoneNumberId, IReadOnlyList<CloudApiMessage> Messages);

public static class CloudApiPayload
{
    public static CloudApiChange Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");
        var phoneNumberId = value.GetProperty("metadata").GetProperty("phone_number_id").GetString()
            ?? throw new InvalidOperationException("phone_number_id is missing.");
        var messages = new List<CloudApiMessage>();
        if (value.TryGetProperty("messages", out var messageArray))
        {
            foreach (var message in messageArray.EnumerateArray())
            {
                string? body = null;
                if (message.TryGetProperty("text", out var text) && text.TryGetProperty("body", out var bodyElement))
                {
                    body = bodyElement.GetString();
                }

                string? ctwaClid = null;
                if (message.TryGetProperty("referral", out var referral) && referral.TryGetProperty("ctwa_clid", out var click))
                {
                    ctwaClid = click.GetString();
                }

                messages.Add(new CloudApiMessage(
                    message.GetProperty("id").GetString() ?? "",
                    message.GetProperty("from").GetString() ?? "",
                    body,
                    ctwaClid));
            }
        }

        return new CloudApiChange(phoneNumberId, messages);
    }
}
