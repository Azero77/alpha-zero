using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;
using ErrorOr;
using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="ICourseQueryService"/> wiring the module's cached read surfaces
/// (catalog, course tree view, course overview public shape, readiness companion) to the
/// platform HybridCache — the same read-through pattern as Identity's
/// <c>CachePublicKeyProvider</c> / <c>CachingTenantUserPrincipalAssignmentRepository</c>.
/// Sibling precedent imitated, no shared-infrastructure changes.
///
/// Conservative default: only the surfaces named by the issue are cached here. Tenant
/// enrollment/dashboard/analytics/pool/admin reads consciously stay uncached (write-path
/// correctness over speed). Without a tenant context the decorator is a pure pass-through —
/// no cache key ever exists without a tenant id.
///
/// Eviction is tag-based and lives in <see cref="CourseCacheEvictionBehavior{TRequest,TResponse}"/>
/// (wired to the exact mutation commands, invalidated only after the commit).
/// </summary>
public class CachedCourseQueryService(
    ICourseQueryService inner,
    HybridCache cache,
    ITenantProvider tenantProvider) : ICourseQueryService
{
    public async Task<PagedResult<CourseSummaryDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null)
            return await inner.ListCoursesAsync(subjectId, page, perPage, cancellationToken);

        return await cache.GetOrCreateAsync<PagedResult<CourseSummaryDto>>(
            CourseCacheKeys.CatalogKey(tenantId, subjectId, page, perPage),
            async token => await inner.ListCoursesAsync(subjectId, page, perPage, token),
            CourseCacheKeys.CatalogEntry,
            tags: [CourseCacheKeys.CatalogTag(tenantId)],
            cancellationToken: cancellationToken);
    }

    public async Task<CourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null)
            return await inner.GetCourseByIdAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync<CourseDto?>(
            CourseCacheKeys.CourseViewKey(tenantId, courseId),
            async token => await inner.GetCourseByIdAsync(courseId, token),
            CourseCacheKeys.CourseViewEntry,
            tags: [CourseCacheKeys.CourseTag(tenantId, courseId)],
            cancellationToken: cancellationToken);
    }

    public async Task<CourseReadiness?> GetCourseReadinessAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null)
            return await inner.GetCourseReadinessAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync<CourseReadiness?>(
            CourseCacheKeys.CourseReadinessKey(tenantId, courseId),
            async token => await inner.GetCourseReadinessAsync(courseId, token),
            CourseCacheKeys.CourseReadinessEntry,
            tags: [CourseCacheKeys.CourseTag(tenantId, courseId), CourseCacheKeys.CourseAssetsTag(tenantId)],
            cancellationToken: cancellationToken);
    }

    public async Task<CourseOverviewDto?> GetCourseOverviewAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null)
            return await inner.GetCourseOverviewAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync<CourseOverviewDto?>(
            CourseCacheKeys.CourseOverviewKey(tenantId, courseId),
            async token => await inner.GetCourseOverviewAsync(courseId, token),
            CourseCacheKeys.CourseOverviewEntry,
            tags: [CourseCacheKeys.CourseTag(tenantId, courseId)],
            cancellationToken: cancellationToken);
    }

    public async Task<List<CourseAssetDto>> GetCoursePoolAsync(
        Guid courseId,
        bool includeAssigned = false,
        CancellationToken cancellationToken = default)
    {
        // Out of scope by the issue's landing zone: the pool is an admin surface.
        return await inner.GetCoursePoolAsync(courseId, includeAssigned, cancellationToken);
    }
}
