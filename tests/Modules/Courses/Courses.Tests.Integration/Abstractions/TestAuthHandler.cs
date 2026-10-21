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
        // Anonymous opt-out (#32/D4): a request carrying `X-Test-Anonymous` runs through the
        // host with NO identity — this is the only way the anonymous catalog/view paths and
        // the real HttpTenantProvider claim/header precedence are exercised end-to-end.
        if (Context.Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Default: random user ids per request. `X-Test-User-Id` pins the caller identity
        // (sub/NameIdentifier) — the first sub claim wins in AuthorizationContextProvider,
        // so the pinned claims must REPLACE the defaults, not be appended.
        var userId = Guid.NewGuid().ToString();
        if (Context.Request.Headers.TryGetValue("X-Test-User-Id", out var pinnedUserId))
        {
            userId = pinnedUserId.ToString();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
            new Claim("auth_method", "TenantUser"),
            new Claim("sid", Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
