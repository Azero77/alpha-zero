using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Modules.Identity.Infrastructure.Serialization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class CachingTenantUserPrincipalAssignmentRepository : TenantUserPrincipalAssignmentRepository
{
    private static readonly CachePolicy Policy = new(TimeSpan.FromMinutes(30));

    private readonly ICachingService _cache;
    private readonly ITenantUserPrincipalAssignmentRepository _inner;

    public CachingTenantUserPrincipalAssignmentRepository(
        AppDbContext context, 
        IPrincipalRepository principalRepository, 
        ITenantUserPrincipalAssignmentRepository inner,
        ICachingService cache) 
        : base(context, principalRepository)
    {
        _inner = inner;
        _cache = cache;
    }

    public override async Task<TenantUserPrincipalAssignment?> GetActiveAssignment(Guid tenantUserId, string? resourceArn = null, CancellationToken ct = default)
    {
        // HybridCache serialises even L1 entries, so the cached value is a JSON-safe projection
        // which is rehydrated into the domain aggregates on every read.
        var cached = await _cache.GetOrCreateAsync(
            IdentityCacheKeys.UserAssignments(tenantUserId.ToString()),
            Policy,
            [],
            async token => CachedAssignmentMapper.ToCache(await _inner.GetAllAssignmentsEagerAsync(tenantUserId, token)),
            ct);

        var assignments = CachedAssignmentMapper.ToDomain(cached);

        if (assignments.Count == 0) return null;

        if (resourceArn is null)
            return assignments.FirstOrDefault();

        var path = ResourceArn.Create(resourceArn).Value.ResourcePath;
        
        return assignments
           .Where(a => a.Resource.IsRequestedPathContainedInResource(path))
           .OrderByDescending(a => a.TimeCreated)
           .FirstOrDefault();
    }

    public override void Add(TenantUserPrincipalAssignment entity)
    {
        _inner.Add(entity);
        _cache.Evict(IdentityCacheKeys.UserAssignments(entity.TenantUser.Id.ToString())); //evict key
    }

    public override void Remove(TenantUserPrincipalAssignment entity)
    {
        _inner.Remove(entity);
        _cache.Evict(IdentityCacheKeys.UserAssignments(entity.TenantUser.Id.ToString())); //evict key
    }
}
