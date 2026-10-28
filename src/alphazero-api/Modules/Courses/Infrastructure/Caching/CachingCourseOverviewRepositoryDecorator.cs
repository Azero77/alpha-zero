using System.Linq.Expressions;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>Write-side cache decorator for course overviews (course tag family).</summary>
public sealed class CachingCourseOverviewRepositoryDecorator : ICourseOverviewRepository
{
    private readonly ICourseOverviewRepository _inner;
    private readonly IEvictionProvider _eviction;

    public CachingCourseOverviewRepositoryDecorator(
        ICourseOverviewRepository inner,
        IEvictionProvider eviction)
    {
        _inner = inner;
        _eviction = eviction;
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

    public Task<CourseOverviewDto?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default) =>
        _inner.GetByCourseIdAsync(courseId, cancellationToken);

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

    public Task<CourseOverview?> GetById(Guid id, CancellationToken token = default) =>
        _inner.GetById(id, token);

    public void IgnoreQueryFilter() => _inner.IgnoreQueryFilter();

    private void Evict(CourseOverview overview)
    {
        foreach (var tag in CourseCacheTags.ForCourseOverview(overview.TenantId, overview.CourseId))
            _eviction.EvictTag(tag);
    }
}
