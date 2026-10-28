using System.Linq.Expressions;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>Write-side cache decorator for the course asset pool.</summary>
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
        foreach (var tag in CourseCacheTags.ForAsset(asset))
            _eviction.EvictTag(tag);
    }
}
