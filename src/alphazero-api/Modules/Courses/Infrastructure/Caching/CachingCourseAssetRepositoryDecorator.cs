using System.Linq.Expressions;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>Write-side cache decorator for the course asset pool: queues the asset row of the
/// eviction matrix from the aggregate argument only (the asset owns the tenant and its course).</summary>
public sealed class CachingCourseAssetRepositoryDecorator : IRepository<CourseAsset>
{
    private readonly IRepository<CourseAsset> _inner;
    private readonly IEvictionProvider _eviction;

    public CachingCourseAssetRepositoryDecorator(
        IRepository<CourseAsset> inner,
        IEvictionProvider eviction)
    {
        _inner = inner;
        _eviction = eviction;
    }

    public void Add(CourseAsset entity)
    {
        Evict(entity);
        _inner.Add(entity);
    }

    public void Remove(CourseAsset entity)
    {
        Evict(entity);
        _inner.Remove(entity);
    }

    public void Update(CourseAsset entity)
    {
        Evict(entity);
        _inner.Update(entity);
    }

    public Task<CourseAsset?> GetFirst(Expression<Func<CourseAsset, bool>> filter, CancellationToken token = default) =>
        _inner.GetFirst(filter, token);

    public Task<IReadOnlyCollection<CourseAsset>> Get(Expression<Func<CourseAsset, bool>> filter, CancellationToken token = default) =>
        _inner.Get(filter, token);

    public Task<IReadOnlyCollection<CourseAsset>> GetAll(CancellationToken token = default) =>
        _inner.GetAll(token);

    public Task<bool> Any(Expression<Func<CourseAsset, bool>> filter, CancellationToken token = default) =>
        _inner.Any(filter, token);

    public Task<int> Count(Expression<Func<CourseAsset, bool>>? filter = null, CancellationToken token = default) =>
        _inner.Count(filter, token);

    public Task<CourseAsset?> GetById(Guid id, CancellationToken token = default) =>
        _inner.GetById(id, token);

    public void IgnoreQueryFilter() => _inner.IgnoreQueryFilter();

    private void Evict(CourseAsset asset)
    {
        if (asset.TenantId == Guid.Empty)
            return; // No tenant → no scoped cache; queue nothing under a placeholder tenant.

        var keys = CourseCacheKeys.EvictCourseAsset(CacheScope.ForTenant(asset.TenantId), asset.CourseId, asset.Id);
        if (keys.Count == 0)
            return; // Unassigned pool asset (CourseId == Guid.Empty): not part of any course surface.

        _eviction.Evict(keys);
    }
}
