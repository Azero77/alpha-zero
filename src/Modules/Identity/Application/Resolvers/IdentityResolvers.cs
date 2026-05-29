using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Identity.Application.Resolvers;

public class IdentityTenantResolver(AppDbContext dbContext) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Identity;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        // Check Principals
        var principalTenant = await dbContext.Principals
            .Where(p => p.Id == resourceId)
            .Select(p => (Guid?)p.TenantId)
            .FirstOrDefaultAsync(ct);
        
        if (principalTenant != null) return principalTenant;

        // Check Policies
        var policyTenant = await dbContext.Policies
            .Where(p => p.Id == resourceId)
            .Select(p => (Guid?)p.TenantId)
            .FirstOrDefaultAsync(ct);

        return policyTenant;
    }
}

public class UserTenantResolver(AppDbContext dbContext) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Users;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        return await dbContext.TenantUsers
            .Where(u => u.Id == resourceId)
            .Select(u => (Guid?)u.TenantId)
            .FirstOrDefaultAsync(ct);
    }
}
