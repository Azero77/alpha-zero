using System.Buffers.Text;
using System.Text.Json;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Security;
using AlphaZero.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure;

/// <summary>
/// Step 5 QA gate (issue #34 §4): the shared access-window ticket signer/verifier round-trips,
/// rejects tampering and expiry, and exposes the frozen access-window helpers. The dedicated
/// secret must fail fast in production and keep the dev default usable in development — same
/// discipline as <see cref="PlaybackTicketSecretValidationTests"/>.
/// </summary>
public class AccessTicketSignerTests
{
    private static IAccessTicketSigner CreateSigner(string? secret = null, int ttlSeconds = 7200)
    {
        var settings = new AccessTicketSettings
        {
            Secret = secret ?? "unit-test-access-ticket-secret-min-32-chars!!!",
            TtlSeconds = ttlSeconds,
        };
        return new AccessTicketSigner(Microsoft.Extensions.Options.Options.Create(settings));
    }

    private static (Guid TenantId, Guid UserId, Guid EnrollmentId) Ids()
        => (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    // ------------------------------------------------------------------
    // Round trip
    // ------------------------------------------------------------------

    [Fact]
    public void Generate_then_verify_Should_roundtrip_payload_exactly()
    {
        var signer = CreateSigner();
        var (tenantId, userId, enrollmentId) = Ids();
        var windowStart = DateTimeOffset.UtcNow.AddDays(-2);
        var windowEnd = DateTimeOffset.UtcNow.AddDays(28);

        var ticket = signer.GenerateTicket(tenantId, userId, enrollmentId, windowStart, windowEnd);

        ticket.Should().MatchRegex("^[A-Za-z0-9_-]+\\.[0-9a-f]+$",
            "header-shaped: base64url(payload) + '.' + lowercase hex(HMAC)");

        var verified = signer.TryVerifyTicket(ticket, out var payload, out var error);

        verified.Should().BeTrue($"round trip must verify (error: {error})");
        payload.Should().NotBeNull();
        payload!.TenantId.Should().Be(tenantId);
        payload.UserId.Should().Be(userId);
        payload.EnrollmentId.Should().Be(enrollmentId);
        payload.WindowStart.Should().Be(windowStart.ToUnixTimeSeconds());
        payload.WindowEnd.Should().Be(windowEnd.ToUnixTimeSeconds());
        payload.Iat.Should().BeGreaterThanOrEqualTo(payload.Exp - 7200 - 5, "the mint stamps iat ~two hours before exp");
        payload.IsExpired.Should().BeFalse("the ticket ttl is ~2h and was minted just now");
    }

    [Fact]
    public void Generate_Should_use_configured_default_ttl()
    {
        var signer = CreateSigner(ttlSeconds: 60);

        var ticket = signer.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
        signer.TryVerifyTicket(ticket, out var payload, out _).Should().BeTrue();
        (payload!.Exp - payload.Iat).Should().BeGreaterThanOrEqualTo(59).And.BeLessThanOrEqualTo(61);
    }

    // ------------------------------------------------------------------
    // Tamper / forgery
    // ------------------------------------------------------------------

    [Fact]
    public void TryVerifyTicket_Should_reject_payload_tampering()
    {
        var signer = CreateSigner();
        var ticket = signer.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        // Canonical forgery attempt: decode the payload, stretch its Exp, re-encode, and keep
        // the ORIGINAL signature — the HMAC must refuse the modified byte string.
        var dot = ticket.IndexOf('.');
        var payloadPart = ticket[..dot];
        var signatureHex = ticket[(dot + 1)..];

        var payload = JsonSerializer.Deserialize<AccessTicketPayload>(Base64Url.DecodeFromChars(payloadPart));
        var tamperedJson = JsonSerializer.SerializeToUtf8Bytes(payload! with { Exp = payload!.Exp + 9999 });
        var tampered = $"{Base64Url.EncodeToString(tamperedJson)}.{signatureHex}";

        var verified = signer.TryVerifyTicket(tampered, out _, out var error);

        verified.Should().BeFalse("any payload mutation breaks the HMAC over those bytes");
        error.Should().Be("Signature mismatch.");
    }

    [Fact]
    public void TryVerifyTicket_Should_reject_signature_tampering()
    {
        var signer = CreateSigner();
        var ticket = signer.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        var dot = ticket.IndexOf('.');
        var signature = ticket[(dot + 1)..];
        var tamperedSig = signature[0] == 'a' ? $"b{signature[1..]}" : $"a{signature[1..]}";
        var tampered = $"{ticket[..dot]}.{tamperedSig}";

        var verified = signer.TryVerifyTicket(tampered, out _, out var error);

        verified.Should().BeFalse();
        error.Should().Be("Signature mismatch.");
    }

    [Fact]
    public void TryVerifyTicket_Should_reject_ticket_from_another_secret()
    {
        var signerA = CreateSigner("secret-A-alphazero-min-32-characters-long!");
        var signerB = CreateSigner("secret-B-alphazero-min-32-characters-long!");

        var ticket = signerA.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        var verified = signerB.TryVerifyTicket(ticket, out _, out var error);

        verified.Should().BeFalse("tickets signed under a different key must not verify");
        error.Should().Be("Signature mismatch.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-signature-part")]
    [InlineData(".")]
    [InlineData("payload.")]
    [InlineData(".signature")]
    [InlineData("payload.!!not-hex!!")]
    public void TryVerifyTicket_Should_reject_malformed_tickets(string ticket)
    {
        var verified = CreateSigner().TryVerifyTicket(ticket, out var payload, out var error);

        verified.Should().BeFalse();
        payload.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    // ------------------------------------------------------------------
    // Expiry (ticket-level)
    // ------------------------------------------------------------------

    [Fact]
    public async Task TryVerifyTicket_Should_fail_after_ticket_expiry()
    {
        // unix-second truncation means sub-second ttls are meaningless — use a 1s ttl and wait
        // past its boundary (mirrors the playback-ticket semantics).
        var signer = CreateSigner();
        var ticket = signer.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), ttl: TimeSpan.FromSeconds(1));

        await Task.Delay(TimeSpan.FromSeconds(2.2));

        var verified = signer.TryVerifyTicket(ticket, out var payload, out var error);
        verified.Should().BeFalse("the ticket-exp must bound the minted proof");
        error.Should().Be("Ticket has expired.");
        payload.Should().BeNull("an expired ticket must not leak its payload as verified data");
    }

    [Fact]
    public void IsExpired_Should_flip_at_exp_boundary()
    {
        var justExpired = new AccessTicketPayload(
            TenantId: Guid.NewGuid(), UserId: Guid.NewGuid(), EnrollmentId: Guid.NewGuid(),
            Exp: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1,
            Iat: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 100,
            WindowStart: 0, WindowEnd: long.MaxValue);
        justExpired.IsExpired.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Access-window helpers (the frozen "they bought N days", §3.2.5)
    // ------------------------------------------------------------------

    [Fact]
    public void IsWindowOpen_Should_report_open_within_and_closed_outside_the_window()
    {
        var windowStart = DateTimeOffset.UtcNow.AddDays(-1);
        var windowEnd = DateTimeOffset.UtcNow.AddDays(29);

        var open = new AccessTicketPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Exp: DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds(),
            Iat: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            WindowStart: windowStart.ToUnixTimeSeconds(),
            WindowEnd: windowEnd.ToUnixTimeSeconds());

        open.IsWindowOpen(DateTimeOffset.UtcNow).Should().BeTrue();
        open.IsWindowOpen(windowStart.AddSeconds(-1)).Should().BeFalse("windowStart is inclusive_lower_bound");
        open.IsWindowOpen(windowEnd).Should().BeTrue("windowEnd is inclusive (the deadline instant itself)");
        open.IsWindowOpen(windowEnd.AddSeconds(1)).Should().BeFalse("the window closes after the access deadline");
    }

    [Fact]
    public void IsWindowOpen_Should_be_false_for_a_ticket_whose_window_never_started_or_already_closed()
    {
        var longGone = new AccessTicketPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Exp: DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds(),
            Iat: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            WindowStart: DateTimeOffset.UtcNow.AddDays(-90).ToUnixTimeSeconds(),
            WindowEnd: DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeSeconds());

        var notYet = new AccessTicketPayload(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Exp: DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds(),
            Iat: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            WindowStart: DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds(),
            WindowEnd: DateTimeOffset.UtcNow.AddDays(40).ToUnixTimeSeconds());

        longGone.IsWindowOpen(DateTimeOffset.UtcNow).Should().BeFalse("the access window already closed");
        notYet.IsWindowOpen(DateTimeOffset.UtcNow).Should().BeFalse("the access window has not started");
    }

    // ------------------------------------------------------------------
    // Configuration discipline (mirrors PlaybackTicketSecretValidationTests)
    // ------------------------------------------------------------------

    [Fact]
    public void Add_WhenAccessTicketSecretMissingAndEnvironmentIsProduction_ThrowsStartupError()
    {
        var services = new ServiceCollection();
        // Satisfy the Cloudflare fail-fast (which runs first) so the ACCESS-ticket gate is the
        // one that throws here; the access secret itself is deliberately absent.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{CloudflareSettings.SectionName}:PlaybackTicketSecret"] = "cloudflare-prod-secret-min-32-chars!!"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var act = () => services.AddSharedInfrastructure(config, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AccessTicket*");
    }

    [Fact]
    public void Add_WhenAccessTicketSecretMissingAndEnvironmentIsDevelopment_StillAcceptsDevSecret()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build(); // no AccessTicket section
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        var act = () => services.AddSharedInfrastructure(config, env);

        act.Should().NotThrow();

        // The dev constant must still work end-to-end: mint + verify through the container.
        using var provider = services.BuildServiceProvider();
        var signer = provider.GetRequiredService<IAccessTicketSigner>();
        var ticket = signer.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
        signer.TryVerifyTicket(ticket, out _, out var error).Should().BeTrue($"dev secret is acceptable in Development (error: {error})");
    }

    [Fact]
    public void Add_WhenAccessTicketSecretConfiguredAndEnvironmentIsProduction_Composes()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Satisfy the Cloudflare gate first so both gates pass together.
                [$"{CloudflareSettings.SectionName}:PlaybackTicketSecret"] = "cloudflare-prod-secret-min-32-chars!!",
                [$"{AccessTicketSettings.SectionName}:{nameof(AccessTicketSettings.Secret)}"] =
                    "configured-prod-access-secret-min-32-chars!!"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var act = () => services.AddSharedInfrastructure(config, env);

        act.Should().NotThrow();
    }
}
