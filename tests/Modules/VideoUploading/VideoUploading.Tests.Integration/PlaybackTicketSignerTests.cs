using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using AlphaZero.Shared.Infrastructure.Security;
using AlphaZero.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class PlaybackTicketSignerTests
{
    private const string TestSecret = "playback-ticket-dedicated-secret-min32bytes!";
    private readonly PlaybackTicketSigner _signer;

    public PlaybackTicketSignerTests()
    {
        var settings = new CloudflareSettings
        {
            PlaybackTicketSecret = TestSecret,
            PlaybackTicketTtlSeconds = 120
        };
        _signer = new PlaybackTicketSigner(Options.Create(settings));
    }

    [Fact]
    public void GenerateTicket_ShouldProduceValidTokenFormatAndVerifySuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var fingerprint = "fp-desktop-client-12345";

        // Act
        var ticket = _signer.GenerateTicket(tenantId, userId, courseId, itemId, videoId, TimeSpan.FromSeconds(120), fingerprint);

        // Assert
        ticket.Should().NotBeNullOrWhiteSpace();
        var parts = ticket.Split('.');
        parts.Should().HaveCount(2);

        // Part 1: Base64Url JSON
        var jsonBytes = Base64Url.DecodeFromChars(parts[0]);
        var payload = JsonSerializer.Deserialize<PlaybackTicketPayload>(jsonBytes);
        payload.Should().NotBeNull();
        payload!.TenantId.Should().Be(tenantId);
        payload.UserId.Should().Be(userId);
        payload.CourseId.Should().Be(courseId);
        payload.ItemId.Should().Be(itemId);
        payload.VideoId.Should().Be(videoId);
        payload.Fingerprint.Should().Be(fingerprint);
        payload.Exp.Should().BeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(110).ToUnixTimeSeconds());

        // Part 2: Hex Signature (HMAC-SHA256 = 32 bytes = 64 hex chars)
        parts[1].Should().HaveLength(64);

        // Round-trip verification
        var isValid = _signer.TryVerifyTicket(ticket, out var verifiedPayload, out var error);
        isValid.Should().BeTrue();
        error.Should().BeNull();
        verifiedPayload.Should().NotBeNull();
        verifiedPayload!.TenantId.Should().Be(tenantId);
        verifiedPayload.UserId.Should().Be(userId);
        verifiedPayload.CourseId.Should().Be(courseId);
        verifiedPayload.ItemId.Should().Be(itemId);
        verifiedPayload.VideoId.Should().Be(videoId);
        verifiedPayload.Fingerprint.Should().Be(fingerprint);
    }

    [Fact]
    public void TryVerifyTicket_ShouldFail_WhenPayloadIsTampered()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var ticket = _signer.GenerateTicket(tenantId, userId, courseId, itemId, videoId);
        var parts = ticket.Split('.');

        // Tamper with payload
        var tamperedPayload = "eyJ0ZXN0IjoidGFtcGVyZWQifQ";
        var tamperedTicket = $"{tamperedPayload}.{parts[1]}";

        // Act
        var isValid = _signer.TryVerifyTicket(tamperedTicket, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().NotBeNull();
        payload.Should().BeNull();
    }

    [Fact]
    public void TryVerifyTicket_ShouldFail_WhenSignatureIsTampered()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var ticket = _signer.GenerateTicket(tenantId, userId, courseId, itemId, videoId);
        var parts = ticket.Split('.');

        // Invert first character of signature
        var invertedFirstChar = parts[1][0] == 'a' ? 'b' : 'a';
        var tamperedSignature = invertedFirstChar + parts[1][1..];
        var tamperedTicket = $"{parts[0]}.{tamperedSignature}";

        // Act
        var isValid = _signer.TryVerifyTicket(tamperedTicket, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Be("Signature mismatch.");
        payload.Should().BeNull();
    }

    [Fact]
    public void TryVerifyTicket_ShouldFail_WhenTicketIsExpired()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var expiredTicket = _signer.GenerateTicket(tenantId, userId, courseId, itemId, videoId, ttl: TimeSpan.FromSeconds(-5));

        // Act
        var isValid = _signer.TryVerifyTicket(expiredTicket, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Be("Ticket has expired.");
        payload.Should().BeNull();
    }

    [Fact]
    public void TryVerifyTicket_ShouldFail_WhenVerifiedWithDifferentSecret()
    {
        // Arrange
        var otherSigner = new PlaybackTicketSigner(Options.Create(new CloudflareSettings
        {
            PlaybackTicketSecret = "completely-different-secret-min-32-chars-long!"
        }));

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var ticket = _signer.GenerateTicket(tenantId, userId, courseId, itemId, videoId);

        // Act
        var isValid = otherSigner.TryVerifyTicket(ticket, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Be("Signature mismatch.");
        payload.Should().BeNull();
    }
}
