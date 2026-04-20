namespace AlphaZero.Shared.Domain;

public interface ICurrentTenantUserRepository
{
    Task<TenantUserDTO?> GetCurrentUser();
}

public record TenantUserDTO(Guid UserId, string IdentityId, string Name, Guid ActiveSessionId);
