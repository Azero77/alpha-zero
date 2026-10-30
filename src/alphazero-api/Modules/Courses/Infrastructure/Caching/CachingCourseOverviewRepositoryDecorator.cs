using System.Linq.Expressions;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Cache decorator for course overviews (spec-33 G11 read-through scope). Writes queue the
/// overview row of the eviction matrix from the entity argument only; the single cacheable
/// repository read — <see cref="GetByCourseIdAsync"/>, a flat, STJ-safe DTO — is read through
/// the <c>CourseOverviewEntity</c> key under the ambient tenant.
/// </summary>
public sealed class CachingCourseOverviewRepositoryDecorator : ICourseOverviewRepository
{
    private readonly ICourseOverviewRepository _inner;
    private readonly IEvictionProvider _eviction;
    private readonly ICachingService _cache;
    private readonly ITenantProvider _tenantProvider;

    public CachingCourseOverviewRepositoryDecorator(
        ICourseOverviewRepository inner,
        IEvictionProvider eviction,
        ICachingService cache,
        ITenantProvider tenantProvider)
    {
        _inner = inner;
        _eviction = eviction;
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public void Add(CourseOverview entity)
    {
        Evict(entity);
        _inner.Add(entity);
    }

    public void Remove(CourseOverview entity)
    {
        Evict(entity);
        _inner.Remove(entity);
    }

    public void Update(CourseOverview entity)
    {
        Evict(entity);
        _inner.Update(entity);
    }

    /// <summary>The only cached single-entity repository read (spec-33 G11): the DTO is a flat
    /// record whose staleness is exclusively coupled to overview writes (which queue the
    /// matching key) — never touches the EF change tracker, never takes caller shape.</summary>
    public Task<CourseOverviewDto?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(_tenantProvider.GetTenant());
        if (!scope.IsEnabled)
            return _inner.GetByCourseIdAsync(courseId, cancellationToken);

        return _cache.GetOrCreateAsync(
            CourseCacheKeys.CourseOverviewEntity(scope, courseId),
            CourseCacheKeys.EntityPolicy,
            [],
            token => _inner.GetByCourseIdAsync(courseId, token),
            cancellationToken);
    }

    public Task<CourseOverview?> GetFirst(Expression<Func<CourseOverview, bool>> filter, CancellationToken token = default) =>
        _inner.GetFirst(filter, token);

    public Task<IReadOnlyCollection<CourseOverview>> Get(Expression<Func<CourseOverview, bool>> filter, CancellationToken token = default) =>
        _inner.Get(filter, token);

    public Task<IReadOnlyCollection<CourseOverview>> GetAll(CancellationToken token = default) =>
        _inner.GetAll(token);

    public Task<bool> Any(Expression<Func<CourseOverview, bool>> filter, CancellationToken token = default) =>
        _inner.Any(filter, token);

    public Task<int> Count(Expression<Func<CourseOverview, bool>>? filter = null, CancellationToken token = default) =>
        _inner.Count(filter, token);

    /// <summary>Uncached by design (spec-33 G12): the upsert handler keeps working from a live
    /// (tracked) entity reference, not from a cached copy.</summary>
    public Task<CourseOverview?> GetById(Guid id, CancellationToken token = default) =>
        _inner.GetById(id, token);

    public void IgnoreQueryFilter() => _inner.IgnoreQueryFilter();

    private void Evict(CourseOverview overview)
    {
        if (overview.TenantId == Guid.Empty)
            return; // No tenant → no scoped cache; queue nothing under a placeholder tenant.

        _eviction.Evict(CourseCacheKeys.EvictCourseOverview(CacheScope.ForTenant(overview.TenantId), overview.CourseId));
    }
}
