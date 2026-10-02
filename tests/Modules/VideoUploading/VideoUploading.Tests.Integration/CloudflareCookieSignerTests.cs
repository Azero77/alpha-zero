using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Shared.Infrastructure.Security;
using AlphaZero.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class CloudflareCookieSignerTests
{
    private const string TestSecret = "super-secret-test-hmac-key-for-video-tokens-123456";
    private readonly CloudflareCookieSigner _signer;

    public CloudflareCookieSignerTests()
    {
        var settings = new CloudflareSettings
        {
            VideoHmacSecret = TestSecret,
            CookieDomain = ".alphazero.academy",
            TokenTtlHours = 4.0,
            Secure = true
        };
        _signer = new CloudflareCookieSigner(Options.Create(settings));
    }

    [Fact]
    public void GenerateSignedCookie_ShouldProduceValidTokenFormatAndProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();

        // Act
        var cookie = _signer.GenerateSignedCookie(tenantId, videoId);

        // Assert
        cookie.CookieName.Should().Be("cf_video_token");
        cookie.Path.Should().Be($"/streaming/{tenantId:D}/{videoId:D}/");
        cookie.Domain.Should().Be(".alphazero.academy");
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be("None");
        cookie.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(3.9));

        var parts = cookie.CookieValue.Split('.');
        parts.Should().HaveCount(2);

        // Part 1: Base64Url JSON
        var jsonBytes = Base64Url.DecodeFromChars(parts[0]);
        var payload = JsonSerializer.Deserialize<CloudflareTokenPayload>(jsonBytes);
        payload.Should().NotBeNull();
        payload!.Vid.Should().Be(videoId);
        payload.Tid.Should().Be(tenantId);
        payload.Path.Should().Be($"/streaming/{tenantId:D}/{videoId:D}/");

        // Part 2: Hex Signature (HMAC-SHA256 = 32 bytes = 64 hex chars)
        parts[1].Should().HaveLength(64);
    }

    [Fact]
    public void TryVerifyToken_ShouldReturnTrue_ForValidToken()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var cookie = _signer.GenerateSignedCookie(tenantId, videoId);

        // Act
        var isValid = _signer.TryVerifyToken(cookie.CookieValue, out var payload, out var error);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
        payload.Should().NotBeNull();
        payload!.Vid.Should().Be(videoId);
        payload.Tid.Should().Be(tenantId);
        payload.Path.Should().Be($"/streaming/{tenantId:D}/{videoId:D}/");
    }

    [Fact]
    public void TryVerifyToken_ShouldFail_WhenPayloadIsTampered()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var cookie = _signer.GenerateSignedCookie(tenantId, videoId);
        var parts = cookie.CookieValue.Split('.');

        // Tamper with payload
        var tamperedPayload = "eyJ0ZXN0IjoidGFtcGVyZWQifQ";
        var tamperedToken = $"{tamperedPayload}.{parts[1]}";

        // Act
        var isValid = _signer.TryVerifyToken(tamperedToken, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Be("Signature mismatch.");
        payload.Should().BeNull();
    }

    [Fact]
    public void TryVerifyToken_ShouldFail_WhenSignatureIsTampered()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var cookie = _signer.GenerateSignedCookie(tenantId, videoId);
        var parts = cookie.CookieValue.Split('.');

        // Change the last character of signature
        var tamperedSig = parts[1][..^1] + (parts[1][^1] == '0' ? '1' : '0');
        var tamperedToken = $"{parts[0]}.{tamperedSig}";

        // Act
        var isValid = _signer.TryVerifyToken(tamperedToken, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Be("Signature mismatch.");
        payload.Should().BeNull();
    }

    [Fact]
    public void TryVerifyToken_ShouldFail_WhenTokenIsExpired()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        // Negative TTL
        var cookie = _signer.GenerateSignedCookie(tenantId, videoId, ttl: TimeSpan.FromSeconds(-10));

        // Act
        var isValid = _signer.TryVerifyToken(cookie.CookieValue, out var payload, out var error);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Be("Token has expired.");
    }

    [Fact]
    public void CrossPlatformCompatibility_KnownTestVector_ShouldMatch()
    {
        // Verify standard HMAC-SHA256 test vector calculation
        var secretBytes = Encoding.UTF8.GetBytes("secret");
        var messageBytes = Encoding.UTF8.GetBytes("hello-cloudflare");

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(messageBytes);
        var hex = Convert.ToHexStringLower(hash);

        // Verified standard HMAC-SHA256("secret", "hello-cloudflare")
        hex.Should().Be("7edee75dd0da4c42175b49455d20fbf1beb7279ce40df50665c3e275136a7e21");
    }
}
