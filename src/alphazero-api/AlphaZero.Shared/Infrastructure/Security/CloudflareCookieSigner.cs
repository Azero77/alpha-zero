using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Shared.Security;
using Microsoft.Extensions.Options;

namespace AlphaZero.Shared.Infrastructure.Security;

public class CloudflareCookieSigner : ICloudflareCookieSigner
{
    public const string CookieName = "cf_video_token";
    private readonly CloudflareSettings _settings;
    private readonly byte[] _keyBytes;

    public CloudflareCookieSigner(IOptions<CloudflareSettings> options)
    {
        _settings = options.Value ?? new CloudflareSettings();
        _keyBytes = Encoding.UTF8.GetBytes(_settings.VideoHmacSecret);
    }

    public CloudflareCookiePayload GenerateSignedCookie(
        Guid tenantId,
        Guid videoId,
        TimeSpan? ttl = null,
        string? clientIp = null)
    {
        var duration = ttl ?? TimeSpan.FromHours(_settings.TokenTtlHours);
        var expiresAt = DateTimeOffset.UtcNow.Add(duration);
        var path = $"/streaming/{tenantId:D}/{videoId:D}/";

        var payload = new CloudflareTokenPayload(
            Path: path,
            Exp: expiresAt.ToUnixTimeSeconds(),
            Vid: videoId,
            Tid: tenantId,
            Ip: clientIp);

        var payloadJsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var base64UrlPayload = Base64Url.EncodeToString(payloadJsonBytes);

        using var hmac = new HMACSHA256(_keyBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(base64UrlPayload));
        var signatureHex = Convert.ToHexStringLower(signatureBytes);

        var token = $"{base64UrlPayload}.{signatureHex}";

        return new CloudflareCookiePayload(
            CookieName: CookieName,
            CookieValue: token,
            Domain: string.IsNullOrWhiteSpace(_settings.CookieDomain) ? null : _settings.CookieDomain,
            Path: path,
            ExpiresAt: expiresAt,
            HttpOnly: true,
            Secure: _settings.Secure,
            SameSite: "None");
    }

    public bool TryVerifyToken(
        string token,
        out CloudflareTokenPayload? payload,
        out string? error)
    {
        payload = null;
        error = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Token is empty.";
            return false;
        }

        var dotIndex = token.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == token.Length - 1)
        {
            error = "Invalid token format. Expected '<payload>.<signature>'.";
            return false;
        }

        var base64UrlPayload = token.Substring(0, dotIndex);
        var signatureHex = token.Substring(dotIndex + 1);

        byte[] providedSigBytes;
        try
        {
            providedSigBytes = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            error = "Invalid signature encoding.";
            return false;
        }

        using var hmac = new HMACSHA256(_keyBytes);
        var expectedSigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(base64UrlPayload));

        if (!CryptographicOperations.FixedTimeEquals(providedSigBytes, expectedSigBytes))
        {
            error = "Signature mismatch.";
            return false;
        }

        try
        {
            var jsonBytes = Base64Url.DecodeFromChars(base64UrlPayload);
            payload = JsonSerializer.Deserialize<CloudflareTokenPayload>(jsonBytes);
            if (payload is null)
            {
                error = "Failed to deserialize token payload.";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"Payload decode error: {ex.Message}";
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.Exp < now)
        {
            error = "Token has expired.";
            return false;
        }

        return true;
    }
}
