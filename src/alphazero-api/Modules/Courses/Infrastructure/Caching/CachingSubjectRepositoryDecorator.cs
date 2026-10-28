using System.Linq.Expressions;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Infrastructure.Caching;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>Write-side cache decorator for subjects (subject listing, catalog and course views).</summary>
public sealed class CachingSubjectRepositoryDecorator : ISubjectRepository
{
    private readonly ISubjectRepository _inner;
    private readonly IEvictionProvider _eviction;

    public CachingSubjectRepositoryDecorator(ISubjectRepository inner, IEvictionProvider eviction)
    {
        _inner = inner;
        _eviction = eviction;
    }

    public void Add(SubjectAggregate entity)
    {
        Evict(entity);
        _inner.Add(entity);
    }

    public void Remove(SubjectAggregate entity)
    {
        Evict(entity);
        _inner.Remove(entity);
    }

    public void Update(SubjectAggregate entity)
    {
        Evict(entity);
        _inner.Update(entity);
    }

    public Task<SubjectAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<SubjectAggregate?> GetFirst(Expression<Func<SubjectAggregate, bool>> filter, CancellationToken token = default) =>
        _inner.GetFirst(filter, token);

    public Task<IReadOnlyCollection<SubjectAggregate>> Get(Expression<Func<SubjectAggregate, bool>> filter, CancellationToken token = default) =>
        _inner.Get(filter, token);

    public Task<IReadOnlyCollection<SubjectAggregate>> GetAll(CancellationToken token = default) =>
        _inner.GetAll(token);

    public Task<bool> Any(Expression<Func<SubjectAggregate, bool>> filter, CancellationToken token = default) =>
        _inner.Any(filter, token);

    public Task<int> Count(Expression<Func<SubjectAggregate, bool>>? filter = null, CancellationToken token = default) =>
        _inner.Count(filter, token);

    public Task<SubjectAggregate?> GetById(Guid id, CancellationToken token = default) =>
        _inner.GetById(id, token);

    public void IgnoreQueryFilter() => _inner.IgnoreQueryFilter();

    private void Evict(SubjectAggregate subject)
    {
        foreach (var tag in CourseCacheTags.ForSubject(subject.TenantId))
            _eviction.EvictTag(tag);
    }
}
