using System.Security.Cryptography;
using System.Text;

namespace TrackLeadFlow.Modules.WhatsApp.Infrastructure.External.CloudApi;

public static class WebhookSignature
{
    public static bool IsValid(string appSecret, string body, string? signatureHeader)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var provided = signatureHeader[prefix.Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}

public interface IChannelAppSecret
{
    string? Find(string phoneNumberId);
}
