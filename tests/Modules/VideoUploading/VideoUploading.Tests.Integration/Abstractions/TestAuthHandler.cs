using System.Security.Claims;
using System.Text.Encodings.Web;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VideoUploading.Tests.Integration.Abstractions;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, 
        ILoggerFactory logger, 
        UrlEncoder encoder) 
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new Claim("auth_method", "TenantUser"),
            new Claim("sid", Guid.NewGuid().ToString()),
            new Claim("identity_id", "test-identity"),
            new Claim("name", "Test User")
        };

        if (Context.Request.Headers.TryGetValue("X-Test-User-Id", out var userId) && Guid.TryParse(userId, out var parsedUserId))
        {
            claims.Add(new Claim("sub", userId.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

            var db = Context.RequestServices.GetService<AppDbContext>();
            if (db != null)
            {
                var tenantUser = await db.TenantUsers.Include(u => u.Devices).FirstOrDefaultAsync(u => u.Id == parsedUserId);
                if (tenantUser != null)
                {
                    if (tenantUser.MainDeviceId.HasValue)
                    {
                        claims.Add(new Claim(CustomClaimTypes.MainDeviceId, tenantUser.MainDeviceId.Value.ToString()));
                    }

                    var reqDevId = Context.Request.Headers["X-Device-Id"].ToString();
                    var matchedDevice = tenantUser.Devices.FirstOrDefault(d => d.Id.ToString() == reqDevId)
                        ?? tenantUser.Devices.FirstOrDefault(d => d.Id == tenantUser.MainDeviceId)
                        ?? tenantUser.Devices.FirstOrDefault();

                    if (matchedDevice != null)
                    {
                        claims.Add(new Claim(CustomClaimTypes.DeviceId, matchedDevice.Id.ToString()));
                        claims.Add(new Claim(CustomClaimTypes.DeviceName, matchedDevice.DeviceName));
                        claims.Add(new Claim(CustomClaimTypes.DevicePlatform, matchedDevice.Platform.ToString()));
                        claims.Add(new Claim(CustomClaimTypes.DevicePublicKey, matchedDevice.PublicKey));
                    }
                }
            }
        }
        else
        {
            var randomId = Guid.NewGuid().ToString();
            claims.Add(new Claim("sub", randomId));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, randomId));
        }

        if (Context.Request.Headers.TryGetValue("X-TenantId", out var tenantIdStr))
        {
            claims.Add(new Claim(CustomClaimTypes.TenantId, tenantIdStr.ToString()));
        }

        if (Context.Request.Headers.TryGetValue("X-Device-Id", out var deviceIdStr) && !claims.Any(c => c.Type == CustomClaimTypes.DeviceId))
        {
            claims.Add(new Claim(CustomClaimTypes.DeviceId, deviceIdStr.ToString()));
        }

        if (Context.Request.Headers.TryGetValue("X-Main-Device-Id", out var mainDeviceIdStr) && !claims.Any(c => c.Type == CustomClaimTypes.MainDeviceId))
        {
            claims.Add(new Claim(CustomClaimTypes.MainDeviceId, mainDeviceIdStr.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return AuthenticateResult.Success(ticket);
    }
}
