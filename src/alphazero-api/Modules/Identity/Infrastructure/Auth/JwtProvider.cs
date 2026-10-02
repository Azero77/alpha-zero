using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Shared.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

public class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;
    private readonly SigningCredentials _signingCredentials;

    public JwtProvider(IOptions<JwtOptions> options, IConfiguration configuration)
    {
        _options = options.Value;

        var secret = _options.Secret;

        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException($"JWT Secret must be at least 256 bits (32 bytes). Current key is {keyBytes.Length * 8} bits.");
        }
        _key = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
    }

    public string GenerateToken(Guid id, Guid tenantId, AuthenticationMethod method, List<ClaimDTO>? addiotionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tid", tenantId.ToString()),
            new("auth_method", method.ToString())
        };
        if(addiotionalClaims != null)
            claims.AddRange(addiotionalClaims.Select(s => new Claim(s.Key,s.Value)));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryInMinutes > 0 ? _options.ExpiryInMinutes : 1440),
            signingCredentials: _signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
