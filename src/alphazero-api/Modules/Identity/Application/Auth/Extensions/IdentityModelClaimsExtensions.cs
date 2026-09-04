using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Application.Auth.Extensions;

public static class IdentityModelClaimsExtensions
{
    public static List<ClaimDTO> ToClaims(this TenantUser user, UserDevice device, IClock? clock = null)
    {
        var claims = new List<ClaimDTO>
        {
            new(CustomClaimTypes.IdentityId, user.IdentityId),
            new(CustomClaimTypes.Name, user.Name),
            new(CustomClaimTypes.DeviceId, device.Id.ToString()),
            new(CustomClaimTypes.DeviceName, device.DeviceName),
            new(CustomClaimTypes.DevicePlatform, device.Platform.ToString()),
            new(CustomClaimTypes.DevicePublicKey, device.PublicKey)
        };

        if (user.MainDeviceId.HasValue)
        {
            claims.Add(new(CustomClaimTypes.MainDeviceId, user.MainDeviceId.Value.ToString()));
        }

        if (clock != null)
        {
            claims.Add(new(CustomClaimTypes.Iat, new DateTimeOffset(clock.Now).ToUnixTimeSeconds().ToString()));
        }

        return claims;
    }

    public static List<ClaimDTO> ToClaims(this Principal principal, IClock? clock = null)
    {
        var claims = new List<ClaimDTO>
        {
            new(CustomClaimTypes.Name, principal.Name),
            new(CustomClaimTypes.Username, principal.Username),
            new(CustomClaimTypes.PrincipalType, principal.PrincipalType.ToString()),
            new(CustomClaimTypes.IsManaged, principal.IsManaged.ToString().ToLowerInvariant()),
            new(CustomClaimTypes.IsGlobal, principal.IsGlobal.ToString().ToLowerInvariant())
        };

        if (principal.PrincipalScope != null)
        {
            claims.Add(new(CustomClaimTypes.PrincipalScope, principal.PrincipalScope.ToString()));
        }

        if (clock != null)
        {
            claims.Add(new(CustomClaimTypes.Iat, new DateTimeOffset(clock.Now).ToUnixTimeSeconds().ToString()));
        }

        return claims;
    }

    public static TenantUserDTO ToDTO(this TenantUser user, UserDevice? currentDevice = null)
    {
        return new TenantUserDTO(
            user.Id,
            user.IdentityId,
            user.Name,
            user.TenantId,
            currentDevice?.Id,
            currentDevice?.DeviceName,
            currentDevice?.Platform.ToString(),
            currentDevice?.PublicKey,
            user.MainDeviceId);
    }

    public static PrincipalDTO ToDTO(this Principal principal)
    {
        return new PrincipalDTO(
            principal.Id,
            principal.TenantId,
            principal.Username,
            principal.Name,
            principal.PrincipalType.ToString(),
            principal.IsManaged,
            principal.IsGlobal,
            principal.PrincipalScope?.ToString());
    }
}
