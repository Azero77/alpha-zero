using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;
using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="ISubjectQueryService"/>: tenant subject listing is cached (longest
/// TTL — subjects change rarely), while <see cref="SubjectQueryService.GetSubjectByIdAsync"/>
/// stays uncached (minor admin query, conservative default of the issue).
/// </summary>
public class CachedSubjectQueryService(
    ISubjectQueryService inner,
    HybridCache cache,
    ITenantProvider tenantProvider) : ISubjectQueryService
{
    public async Task<PagedResult<SubjectDto>> ListSubjectsAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null)
            return await inner.ListSubjectsAsync(page, perPage, cancellationToken);

        return await cache.GetOrCreateAsync<PagedResult<SubjectDto>>(
            CourseCacheKeys.SubjectListKey(tenantId, page, perPage),
            async token => await inner.ListSubjectsAsync(page, perPage, token),
            CourseCacheKeys.SubjectListEntry,
            tags: [CourseCacheKeys.SubjectsTag(tenantId)],
            cancellationToken: cancellationToken);
    }

    public async Task<SubjectDto?> GetSubjectByIdAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        return await inner.GetSubjectByIdAsync(subjectId, cancellationToken);
    }
}
