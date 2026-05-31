using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Identity.Application.Resolvers;

public class IdentityTenantResolver(IPrincipalRepository principalRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Identity;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        // Check Principals
        var principal = await principalRepository.GetById(resourceId);
        if (principal != null) return principal.TenantId;

        return null;
    }
}

public class UserTenantResolver(IRepository<TenantUser> userRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Users;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var user = await userRepository.GetById(resourceId);
        return user?.TenantId;
    }
}
