using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Shared.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

public class JwtProvider : IJwtProvider
{
    private readonly IConfiguration _configuration;

    public JwtProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Guid id, Guid tenantId, AuthenticationMethod method)
    {
        var claims = new List<Claim>
        {
            new Claim("sub", id.ToString()),
            new Claim("tid", tenantId.ToString()),
            new Claim("auth_method", method.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? "a-very-secret-key-that-should-be-in-appsettings"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:InternalIssuer"] ?? "AlphaZero",
            audience: _configuration["Jwt:InternalAudience"] ?? "AlphaZeroClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
