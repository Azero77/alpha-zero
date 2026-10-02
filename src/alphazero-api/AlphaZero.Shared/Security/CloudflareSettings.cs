namespace AlphaZero.Shared.Security;

public class CloudflareSettings
{
    public const string SectionName = "Cloudflare";

    /// <summary>
    /// HMAC-SHA256 Secret key for signing edge video capability tokens.
    /// Must match the HMAC_SECRET in Cloudflare Worker video-auth-worker.js.
    /// </summary>
    public string VideoHmacSecret { get; set; } = "dev-cloudflare-video-hmac-secret-alphazero-default-change-in-prod-min32!";

    /// <summary>
    /// Base cookie domain (e.g., ".alphazero.academy"). In local dev, leave null/empty.
    /// </summary>
    public string? CookieDomain { get; set; } = null;

    /// <summary>
    /// Default token validity in hours. Default: 4 hours.
    /// </summary>
    public double TokenTtlHours { get; set; } = 4.0;

    /// <summary>
    /// Whether edge capability cookies require the Secure flag. Default: true.
    /// </summary>
    public bool Secure { get; set; } = true;
}
