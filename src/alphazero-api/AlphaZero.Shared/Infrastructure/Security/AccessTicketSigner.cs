using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Shared.Security;
using Microsoft.Extensions.Options;

namespace AlphaZero.Shared.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 signer for course access-window tickets (issue #34 rework §3.2.3, step 5) — a
/// structural clone of <see cref="PlaybackTicketSigner"/> on a DEDICATED secret
/// (<c>AccessTicket:Secret</c>): the two ticket kinds are independent capabilities and must never
/// share (or stand in for) each other's signing keys.
/// </summary>
public class AccessTicketSigner : IAccessTicketSigner
{
    private readonly AccessTicketSettings _settings;
    private readonly byte[] _keyBytes;

    public AccessTicketSigner(IOptions<AccessTicketSettings> options)
    {
        _settings = options.Value ?? new AccessTicketSettings();
        _keyBytes = Encoding.UTF8.GetBytes(_settings.Secret);
    }

    public string GenerateTicket(
        Guid tenantId,
        Guid userId,
        Guid enrollmentId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        TimeSpan? ttl = null)
    {
        var duration = ttl ?? TimeSpan.FromSeconds(_settings.TtlSeconds > 0 ? _settings.TtlSeconds : 7200);
        var now = DateTimeOffset.UtcNow;

        var payload = new AccessTicketPayload(
            TenantId: tenantId,
            UserId: userId,
            EnrollmentId: enrollmentId,
            Exp: now.Add(duration).ToUnixTimeSeconds(),
            Iat: now.ToUnixTimeSeconds(),
            WindowStart: windowStart.ToUnixTimeSeconds(),
            WindowEnd: windowEnd.ToUnixTimeSeconds());

        var payloadJsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var base64UrlPayload = Base64Url.EncodeToString(payloadJsonBytes);

        using var hmac = new HMACSHA256(_keyBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(base64UrlPayload));
        var signatureHex = Convert.ToHexStringLower(signatureBytes);

        return $"{base64UrlPayload}.{signatureHex}";
    }

    public bool TryVerifyTicket(
        string ticket,
        out AccessTicketPayload? payload,
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
            payload = JsonSerializer.Deserialize<AccessTicketPayload>(jsonBytes);
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
