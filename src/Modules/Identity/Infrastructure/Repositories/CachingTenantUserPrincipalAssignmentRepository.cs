using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class CachingTenantUserPrincipalAssignmentRepository : TenantUserPrincipalAssignmentRepository
{
    private readonly IMemoryCache _cache;

    public CachingTenantUserPrincipalAssignmentRepository(AppDbContext context, IPrincipalRepository principalRepository, IMemoryCache cache) 
        : base(context, principalRepository)
    {
        _cache = cache;
    }

    public override async Task<TenantUserPrincipalAssignment?> GetActiveAssignment(Guid tenantUserId, string? resourceArn = null, CancellationToken ct = default)
    {
        var cacheKey = $"user_assignments:{tenantUserId}";
        var assignments = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await GetAllAssignmentsEagerAsync(tenantUserId, ct);
        });

        if (assignments is null || !assignments.Any()) return null;

        if (resourceArn is null)
            return assignments.FirstOrDefault();

        var path = ResourceArn.Create(resourceArn).Value.ResourcePath;
        
        return assignments
           .Where(a => a.Resource.IsRequestedPathContainedInResource(path))
           .OrderByDescending(a => a.TimeCreated)
           .FirstOrDefault();
    }
}
