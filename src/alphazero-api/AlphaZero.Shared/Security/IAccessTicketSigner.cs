using System.Text.Json.Serialization;

namespace AlphaZero.Shared.Security;

/// <summary>
/// The access-window ticket (issue #34 rework §3.2.3): a STATELESS capability token proving
/// "user @tenant may access the enrollment's course content until ticket-exp, and whose access
/// window is [windowStart, windowEnd)". Sent in its own header (NOT the JWT) — see
/// <see cref="AccessTicketHttpHeader"/>. The minting endpoint (Courses, step 10) is the stateful
/// gate; validation downstream (the Identity window operator, §3.3) touches NO database.
/// </summary>
public record AccessTicketPayload(
    [property: JsonPropertyName("tid")] Guid TenantId,
    [property: JsonPropertyName("uid")] Guid UserId,
    [property: JsonPropertyName("eid")] Guid EnrollmentId,
    [property: JsonPropertyName("exp")] long Exp,
    [property: JsonPropertyName("iat")] long Iat,
    [property: JsonPropertyName("windowStart")] long WindowStart,
    [property: JsonPropertyName("windowEnd")] long WindowEnd)
{
    public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= Exp;

    /// <summary>
    /// The frozen per-enrollment access window ("they bought N days", §3.2.5): the operator reads
    /// token properties ONLY — an elapsed window here is how expiry is MARKED (path a, §3.4),
    /// while ticket-exp governs how long THIS minted proof lasts (~2h re-mint bound, §2 Q3-R2 TTL).
    /// </summary>
    public bool IsWindowOpen(DateTimeOffset now) => now.ToUnixTimeSeconds() >= WindowStart && now.ToUnixTimeSeconds() <= WindowEnd;
}

/// <summary>Header the access ticket travels in — deliberately NOT the JWT/AUTH header (§3.2.3).</summary>
public static class AccessTicketHttpHeader
{
    public const string Name = "X-Access-Ticket";
}

public interface IAccessTicketSigner
{
    /// <summary>
    /// Generates a signed access-window ticket.
    /// Format: ${base64Url(JSON(payload))}.${hex(HMAC_SHA256(secret, base64Url(JSON(payload))))}
    /// on a DEDICATED secret (<c>AccessTicket:Secret</c>) — never the playback or CDN secret.
    /// </summary>
    string GenerateTicket(
        Guid tenantId,
        Guid userId,
        Guid enrollmentId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        TimeSpan? ttl = null);

    /// <summary>
    /// Validates signature and ticket-exp (fixed-time compare). Does NOT judge the access
    /// window itself — the window belongs to the calling policy/operator (§3.3).
    /// </summary>
    bool TryVerifyTicket(
        string ticket,
        out AccessTicketPayload? payload,
        out string? error);
}
