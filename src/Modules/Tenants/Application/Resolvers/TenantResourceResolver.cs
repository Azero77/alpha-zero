using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Tenants.Application.Resolvers;

public class TenantResourceResolver(ITenantRepository tenantRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Tenants;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        // For a Tenant resource, the ID is the TenantId itself.
        // We verify it exists and return it.
        var tenant = await tenantRepository.GetById(resourceId);
        return tenant?.Id;
    }
}
