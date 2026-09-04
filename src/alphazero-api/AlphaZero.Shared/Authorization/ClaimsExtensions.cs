using System.Security.Claims;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Shared.Authorization;

public static class ClaimsExtensions
{
    public static TenantUserDTO? ToTenantUserDTO(this ClaimsPrincipal? principal)
    {
        if (principal == null) return null;
        return principal.Claims.ToTenantUserDTO();
    }

    public static TenantUserDTO? ToTenantUserDTO(this IEnumerable<Claim>? claims)
    {
        if (claims == null) return null;

        var claimList = claims as IReadOnlyList<Claim> ?? claims.ToList();

        var subClaim = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.Sub)?.Value;
        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
            return null;

        var identityId = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.IdentityId)?.Value;
        if (string.IsNullOrEmpty(identityId))
            return null;

        var name = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.Name)?.Value ?? string.Empty;

        var tenantIdStr = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.TenantId)?.Value;
        Guid? tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : null;

        var deviceIdStr = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.DeviceId)?.Value;
        Guid? deviceId = Guid.TryParse(deviceIdStr, out var did) ? did : null;

        var deviceName = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.DeviceName)?.Value;
        var devicePlatform = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.DevicePlatform)?.Value;
        var publicKey = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.DevicePublicKey)?.Value;

        var mainDeviceIdStr = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.MainDeviceId)?.Value;
        Guid? mainDeviceId = Guid.TryParse(mainDeviceIdStr, out var mdid) ? mdid : null;

        return new TenantUserDTO(
            userId,
            identityId,
            name,
            tenantId,
            deviceId,
            deviceName,
            devicePlatform,
            publicKey,
            mainDeviceId);
    }

    public static PrincipalDTO? ToPrincipalDTO(this ClaimsPrincipal? principal)
    {
        if (principal == null) return null;
        return principal.Claims.ToPrincipalDTO();
    }

    public static PrincipalDTO? ToPrincipalDTO(this IEnumerable<Claim>? claims)
    {
        if (claims == null) return null;

        var claimList = claims as IReadOnlyList<Claim> ?? claims.ToList();

        var subClaim = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.Sub)?.Value;
        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var principalId))
            return null;

        var tenantIdStr = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.TenantId)?.Value;
        Guid tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;

        var username = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.Username)?.Value ?? string.Empty;
        var name = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.Name)?.Value ?? string.Empty;
        var principalType = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.PrincipalType)?.Value ?? "User";

        var isManagedStr = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.IsManaged)?.Value;
        bool isManaged = bool.TryParse(isManagedStr, out var im) && im;

        var isGlobalStr = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.IsGlobal)?.Value;
        bool isGlobal = bool.TryParse(isGlobalStr, out bool ig) ? ig : (tenantId == Guid.Empty);

        var principalScope = claimList.FirstOrDefault(c => c.Type == CustomClaimTypes.PrincipalScope)?.Value;

        return new PrincipalDTO(
            principalId,
            tenantId,
            username,
            name,
            principalType,
            isManaged,
            isGlobal,
            principalScope);
    }

    public static List<ClaimDTO> ToClaims(this TenantUserDTO dto, IClock? clock = null)
    {
        var claims = new List<ClaimDTO>
        {
            new(CustomClaimTypes.IdentityId, dto.IdentityId),
            new(CustomClaimTypes.Name, dto.Name)
        };

        if (dto.TenantId.HasValue)
            claims.Add(new(CustomClaimTypes.TenantId, dto.TenantId.Value.ToString()));

        if (dto.DeviceId.HasValue)
            claims.Add(new(CustomClaimTypes.DeviceId, dto.DeviceId.Value.ToString()));

        if (!string.IsNullOrEmpty(dto.DeviceName))
            claims.Add(new(CustomClaimTypes.DeviceName, dto.DeviceName));

        if (!string.IsNullOrEmpty(dto.DevicePlatform))
            claims.Add(new(CustomClaimTypes.DevicePlatform, dto.DevicePlatform));

        if (!string.IsNullOrEmpty(dto.PublicKey))
            claims.Add(new(CustomClaimTypes.DevicePublicKey, dto.PublicKey));

        if (dto.MainDeviceId.HasValue)
            claims.Add(new(CustomClaimTypes.MainDeviceId, dto.MainDeviceId.Value.ToString()));

        if (clock != null)
            claims.Add(new(CustomClaimTypes.Iat, new DateTimeOffset(clock.Now).ToUnixTimeSeconds().ToString()));

        return claims;
    }

    public static List<ClaimDTO> ToClaims(this PrincipalDTO dto, IClock? clock = null)
    {
        var claims = new List<ClaimDTO>
        {
            new(CustomClaimTypes.Name, dto.Name),
            new(CustomClaimTypes.Username, dto.Username),
            new(CustomClaimTypes.PrincipalType, dto.PrincipalType),
            new(CustomClaimTypes.IsManaged, dto.IsManaged.ToString().ToLowerInvariant()),
            new(CustomClaimTypes.IsGlobal, dto.IsGlobal.ToString().ToLowerInvariant())
        };

        if (!string.IsNullOrEmpty(dto.PrincipalScope))
            claims.Add(new(CustomClaimTypes.PrincipalScope, dto.PrincipalScope));

        if (clock != null)
            claims.Add(new(CustomClaimTypes.Iat, new DateTimeOffset(clock.Now).ToUnixTimeSeconds().ToString()));

        return claims;
    }
}

