using System.Linq.Expressions;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Write-side cache decorator for the course aggregate. Every curriculum mutation is persisted
/// through the root, so the affected key set is derivable from the entity argument alone — no
/// change-tracker scan, no parent lookup, no tenant lookup (the aggregate owns the tenant).
/// Reads pass through: aggregate <c>GetById</c> is a tracked-identity bridge and is never cached.
/// </summary>
public sealed class CachingCourseRepositoryDecorator : ICourseRepository
{
    private readonly ICourseRepository _inner;
    private readonly IEvictionProvider _eviction;

    public CachingCourseRepositoryDecorator(ICourseRepository inner, IEvictionProvider eviction)
    {
        _inner = inner;
        _eviction = eviction;
    }

    public void Add(Course entity)
    {
        Evict(entity);
        _inner.Add(entity);
    }

    public void Remove(Course entity)
    {
        Evict(entity);
        _inner.Remove(entity);
    }

    public void Update(Course entity)
    {
        Evict(entity);
        _inner.Update(entity);
    }

    public ICourseQueryBuilder Query() => _inner.Query();

    public ICourseQueryBuilder Query(Guid courseId) => _inner.Query(courseId);

    public Task<bool> IsAssetInUseAsync(Guid courseAssetId, CancellationToken cancellationToken = default) =>
        _inner.IsAssetInUseAsync(courseAssetId, cancellationToken);

    public Task<Course?> GetFirst(Expression<Func<Course, bool>> filter, CancellationToken token = default) =>
        _inner.GetFirst(filter, token);

    public Task<IReadOnlyCollection<Course>> Get(Expression<Func<Course, bool>> filter, CancellationToken token = default) =>
        _inner.Get(filter, token);

    public Task<IReadOnlyCollection<Course>> GetAll(CancellationToken token = default) =>
        _inner.GetAll(token);

    public Task<bool> Any(Expression<Func<Course, bool>> filter, CancellationToken token = default) =>
        _inner.Any(filter, token);

    public Task<int> Count(Expression<Func<Course, bool>>? filter = null, CancellationToken token = default) =>
        _inner.Count(filter, token);

    public Task<Course?> GetById(Guid id, CancellationToken token = default) =>
        _inner.GetById(id, token);

    public void IgnoreQueryFilter() => _inner.IgnoreQueryFilter();

    private void Evict(Course course)
    {
        if (course.TenantId == Guid.Empty)
            return; // No tenant → no scoped cache; queue nothing under a placeholder tenant.

        _eviction.Evict(CourseCacheKeys.EvictCourse(CacheScope.ForTenant(course.TenantId), course.Id));
    }
}
