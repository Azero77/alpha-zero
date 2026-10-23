using AlphaZero.Shared.Infrastructure.Tenats;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AlphaZero.Modules.Tenants.UnitTests.Infrastructure;

// #32 R1/D2 — HttpTenantProvider tenant precedence. These unit tests pin the full approved
// matrix: a parseable non-empty `tid` claim is the tenant authority; claim absent/empty/global
// falls back to the X-TenantId header; the claim/header mismatch no longer voids the request;
// an unparseable header (when the header is what is needed) fails closed to null.
public class HttpTenantProviderTests
{
    private const string TenantClaimType = "tid";
    private const string TenantHeaderName = "X-TenantId";

    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static HttpTenantProvider CreateProvider(HttpContext context) =>
        new HttpTenantProvider(new HttpContextAccessor { HttpContext = context });

    private static HttpContext Context(string? headerValue = null, string? tidClaimValue = null)
    {
        var context = new DefaultHttpContext();

        if (headerValue is not null)
        {
            context.Request.Headers[TenantHeaderName] = headerValue;
        }

        if (tidClaimValue is not null)
        {
            var identity = new ClaimsIdentity(
                [new Claim(TenantClaimType, tidClaimValue)], authenticationType: "TestScheme");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }

    [Fact]
    public void ParseableNonEmptyTidClaim_Should_Win_OverAnyHeader()
    {
        // A real tenant identity in the token is authoritative — the header never
        // overrides it, and the old mismatch→null voiding is gone.
        var provider = CreateProvider(Context(headerValue: TenantB.ToString(), tidClaimValue: TenantA.ToString()));

        provider.GetTenant().Should().Be(TenantA);
    }

    [Fact]
    public void TidClaimMismatchingHeader_Should_NotVoidTheRequest_TokenRemainsAuthoritative()
    {
        // Regression contract for the deleted mismatch→null branch: a claim/header
        // disagreement used to void the request (null tenant → everything masked).
        var provider = CreateProvider(Context(headerValue: Guid.NewGuid().ToString(), tidClaimValue: TenantA.ToString()));

        provider.GetTenant().Should().Be(TenantA);
    }

    [Fact]
    public void ClaimWithEmptyGuid_Should_CountAsGlobalPrincipal_HeaderIsAuthoritative()
    {
        // Global principals carry tid == Guid.Empty — that is NOT a tenant identity;
        // the header names the tenant whose resource is being accessed.
        var provider = CreateProvider(Context(
            headerValue: TenantB.ToString(),
            tidClaimValue: Guid.Empty.ToString()));

        provider.GetTenant().Should().Be(TenantB);
    }

    [Fact]
    public void MissingTidClaim_Should_FallBackToHeader()
    {
        // Anonymous callers have no token at all — the X-TenantId header is the
        // sole tenant source (the anonymous catalog/view surface, D2).
        var provider = CreateProvider(Context(headerValue: TenantB.ToString()));

        provider.GetTenant().Should().Be(TenantB);
    }

    [Fact]
    public void UnparseableHeader_WhenNeeded_Should_FailClosedToNull()
    {
        // Garbage header with no usable claim: never guess a tenant.
        var provider = CreateProvider(Context(headerValue: "not-a-guid"));

        provider.GetTenant().Should().BeNull();
    }

    [Fact]
    public void NoClaimAndNoHeader_Should_ReturnNull()
    {
        var provider = CreateProvider(new DefaultHttpContext());

        provider.GetTenant().Should().BeNull();
    }

    [Fact]
    public void GuidHeaderUnderGlobalPrincipal_Should_StillFailClosed()
    {
        // A global principal (tid == Guid.Empty) whose header is ALSO the empty guid
        // resolves no tenant: Guid.Empty is not a real tenant.
        var provider = CreateProvider(Context(
            headerValue: Guid.Empty.ToString(),
            tidClaimValue: Guid.Empty.ToString()));

        provider.GetTenant().Should().BeNull();
    }
}
