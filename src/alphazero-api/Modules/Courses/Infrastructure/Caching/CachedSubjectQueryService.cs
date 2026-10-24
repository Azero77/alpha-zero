using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="ISubjectQueryService"/>: the default subject listing is cached
/// (longest TTL — subjects change rarely); non-default paging and the by-id read stay
/// uncached.
/// </summary>
public class CachedSubjectQueryService(
    ISubjectQueryService inner,
    ICachingService cache,
    ITenantProvider tenantProvider) : ISubjectQueryService
{
    public async Task<PagedResult<SubjectDto>> ListSubjectsAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled || !CourseCacheKeys.IsDefaultPage(page, perPage))
            return await inner.ListSubjectsAsync(page, perPage, cancellationToken);

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.SubjectList(scope),
            CourseCacheKeys.SubjectListPolicy,
            [CourseCacheKeys.SubjectsTag(scope)],
            token => inner.ListSubjectsAsync(page, perPage, token),
            cancellationToken);
    }

    public async Task<SubjectDto?> GetSubjectByIdAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        return await inner.GetSubjectByIdAsync(subjectId, cancellationToken);
    }
}
