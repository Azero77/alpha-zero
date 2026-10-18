using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Courses.Tests.Integration.Abstractions;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, 
        ILoggerFactory logger, 
        UrlEncoder encoder) 
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // X-Test-User-Id pins a stable user identity across calls (token/sub/identity_id);
        // requests without the header get a random per-request identity.
        var headerHasUser = Context.Request.Headers.TryGetValue("X-Test-User-Id", out var userId);
        var effectiveUserId = headerHasUser ? userId.First().ToString() : Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, effectiveUserId),
            new Claim("sub", effectiveUserId),
            new Claim("identity_id", effectiveUserId),
            new Claim("name", "Test User"),
            new Claim("auth_method", "TenantUser"),
            new Claim("sid", Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
