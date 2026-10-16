using System.Text.Json.Serialization;

namespace AlphaZero.Shared.Security;

public record PlaybackTicketPayload(
    [property: JsonPropertyName("tid")] Guid TenantId,
    [property: JsonPropertyName("uid")] Guid UserId,
    [property: JsonPropertyName("cid")] Guid CourseId,
    [property: JsonPropertyName("iid")] Guid ItemId,
    [property: JsonPropertyName("vid")] Guid VideoId,
    [property: JsonPropertyName("exp")] long Exp,
    [property: JsonPropertyName("fp")] string? Fingerprint = null)
{
    public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= Exp;
}

public interface IPlaybackTicketSigner
{
    /// <summary>
    /// Generates a signed capability playback ticket.
    /// Format: ${base64Url(JSON(payload))}.${hex(HMAC_SHA256(secret, base64Url(JSON(payload))))}
    /// </summary>
    string GenerateTicket(
        Guid tenantId,
        Guid userId,
        Guid courseId,
        Guid itemId,
        Guid videoId,
        TimeSpan? ttl = null,
        string? fingerprint = null);

    /// <summary>
    /// Validates a capability playback ticket signature and expiration.
    /// </summary>
    bool TryVerifyTicket(
        string ticket,
        out PlaybackTicketPayload? payload,
        out string? error);
}
