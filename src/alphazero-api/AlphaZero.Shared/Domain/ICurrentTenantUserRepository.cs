namespace AlphaZero.Shared.Domain;

public interface ICurrentTenantUserRepository
{
    Task<TenantUserDTO?> GetCurrentUser();
}

public record TenantUserDTO(
    Guid UserId, 
    string IdentityId, 
    string Name,
    Guid? TenantId = null,
    Guid? DeviceId = null,
    string? DeviceName = null,
    string? DevicePlatform = null,
    string? PublicKey = null,
    Guid? MainDeviceId = null);

public record PrincipalDTO(
    Guid PrincipalId,
    Guid TenantId,
    string Username,
    string Name,
    string PrincipalType,
    bool IsManaged,
    bool IsGlobal,
    string? PrincipalScope = null);
