namespace AlphaZero.Shared.Security;

/// <summary>
/// Configuration for the course access-window ticket (issue #34 rework §3.2.3/§3.2.4). The
/// secret is dedicated — isolated from the Cloudflare edge secret and the playback-ticket secret.
/// </summary>
public class AccessTicketSettings
{
    public const string SectionName = "AccessTicket";

    /// <summary>
    /// Dedicated HMAC-SHA256 secret for course access tickets (minted by Courses, validated by
    /// the Identity window operator over a header — no DB in the flight path).
    /// </summary>
    public string Secret { get; set; } = "dev-access-ticket-secret-alphazero-default-change-in-prod-min32!";

    /// <summary>
    /// Ticket validity in seconds. Default ~2 hours (§3.2.4): re-minting is the staleness bound —
    /// while a ticket is valid the edge lets requests pass WITHOUT querying the database, and a
    /// fresh mint re-checks enrollment status at the stateful gate.
    /// </summary>
    public int TtlSeconds { get; set; } = 7200;
}
