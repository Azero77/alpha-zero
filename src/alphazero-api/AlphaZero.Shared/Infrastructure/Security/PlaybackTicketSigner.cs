using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Shared.Security;
using Microsoft.Extensions.Options;

namespace AlphaZero.Shared.Infrastructure.Security;

public class PlaybackTicketSigner : IPlaybackTicketSigner
{
    private readonly CloudflareSettings _settings;
    private readonly byte[] _keyBytes;

    public PlaybackTicketSigner(IOptions<CloudflareSettings> options)
    {
        _settings = options.Value ?? new CloudflareSettings();
        _keyBytes = Encoding.UTF8.GetBytes(_settings.PlaybackTicketSecret);
    }

    public string GenerateTicket(
        Guid tenantId,
        Guid userId,
        Guid courseId,
        Guid itemId,
        Guid videoId,
        TimeSpan? ttl = null,
        string? fingerprint = null)
    {
        var duration = ttl ?? TimeSpan.FromSeconds(_settings.PlaybackTicketTtlSeconds > 0 ? _settings.PlaybackTicketTtlSeconds : 120);
        var expiresAt = DateTimeOffset.UtcNow.Add(duration);

        var payload = new PlaybackTicketPayload(
            TenantId: tenantId,
            UserId: userId,
            CourseId: courseId,
            ItemId: itemId,
            VideoId: videoId,
            Exp: expiresAt.ToUnixTimeSeconds(),
            Fingerprint: fingerprint);

        var payloadJsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var base64UrlPayload = Base64Url.EncodeToString(payloadJsonBytes);

        using var hmac = new HMACSHA256(_keyBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(base64UrlPayload));
        var signatureHex = Convert.ToHexStringLower(signatureBytes);

        return $"{base64UrlPayload}.{signatureHex}";
    }

    public bool TryVerifyTicket(
        string ticket,
        out PlaybackTicketPayload? payload,
        out string? error)
    {
        payload = null;
        error = null;

        if (string.IsNullOrWhiteSpace(ticket))
        {
            error = "Ticket is empty.";
            return false;
        }

        var dotIndex = ticket.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == ticket.Length - 1)
        {
            error = "Invalid ticket format. Expected '<payload>.<signature>'.";
            return false;
        }

        var base64UrlPayload = ticket.Substring(0, dotIndex);
        var signatureHex = ticket.Substring(dotIndex + 1);

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
            payload = JsonSerializer.Deserialize<PlaybackTicketPayload>(jsonBytes);
            if (payload is null)
            {
                error = "Failed to deserialize ticket payload.";
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
            payload = null;
            error = "Ticket has expired.";
            return false;
        }

        return true;
    }
}
