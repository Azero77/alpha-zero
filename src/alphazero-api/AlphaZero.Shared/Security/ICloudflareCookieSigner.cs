using System.Text.Json.Serialization;

namespace AlphaZero.Shared.Security;

public record CloudflareTokenPayload(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("exp")] long Exp,
    [property: JsonPropertyName("vid")] Guid Vid,
    [property: JsonPropertyName("tid")] Guid Tid,
    [property: JsonPropertyName("ip")] string? Ip = null);

public record CloudflareCookiePayload(
    string CookieName,
    string CookieValue,
    string? Domain,
    string Path,
    DateTimeOffset ExpiresAt,
    bool HttpOnly,
    bool Secure,
    string SameSite);

public interface ICloudflareCookieSigner
{
    /// <summary>
    /// Generates a signed edge capability cookie for Cloudflare CDN segment delivery.
    /// Format: ${base64Url(JSON(payload))}.${hex(HMAC_SHA256(secret, base64Url(JSON(payload))))}
    /// </summary>
    CloudflareCookiePayload GenerateSignedCookie(
        Guid tenantId,
        Guid videoId,
        TimeSpan? ttl = null,
        string? clientIp = null);

    /// <summary>
    /// Validates an edge capability token signature and expiration.
    /// </summary>
    bool TryVerifyToken(
        string token,
        out CloudflareTokenPayload? payload,
        out string? error);
}
