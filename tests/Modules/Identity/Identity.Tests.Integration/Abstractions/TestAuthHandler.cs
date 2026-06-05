using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Tests.Integration.Abstractions;

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
        var claims = new List<Claim>
        {
            new Claim("auth_method", "TenantUser"),
            new Claim("sid", Guid.NewGuid().ToString())
        };

        if (Context.Request.Headers.TryGetValue("X-Test-User-Id", out var userId))
        {
            claims.Add(new Claim("sub", userId.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }
        else
        {
            var randomId = Guid.NewGuid().ToString();
            claims.Add(new Claim("sub", randomId));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, randomId));
        }

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
