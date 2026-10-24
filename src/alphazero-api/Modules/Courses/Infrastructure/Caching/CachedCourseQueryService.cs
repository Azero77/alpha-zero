using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="ICourseQueryService"/> wiring the module's cached read surfaces
/// (catalog, public/student course view, course tree view, overview and readiness) to the
/// shared caching abstraction. Without a tenant the decorator is a pure pass-through: no key
/// or tag is ever created without a tenant. Reads whose paging/filter shape is not the default
/// also bypass the cache, so request parameters never enter cache keys.
/// </summary>
public class CachedCourseQueryService(
    ICourseQueryService inner,
    ICachingService cache,
    ITenantProvider tenantProvider) : ICourseQueryService
{
    public async Task<PagedResult<CourseSummaryDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled || subjectId is not null || !CourseCacheKeys.IsDefaultPage(page, perPage))
            return await inner.ListCoursesAsync(subjectId, page, perPage, cancellationToken);

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.StaffCatalog(scope),
            CourseCacheKeys.CatalogPolicy,
            [CourseCacheKeys.CatalogTag(scope)],
            token => inner.ListCoursesAsync(subjectId, page, perPage, token),
            cancellationToken);
    }

    public async Task<PagedResult<CourseCatalogItemDto>> ListCourseCatalogAsync(
        string? search,
        Guid? subjectId,
        string? sort,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled
            || !string.IsNullOrWhiteSpace(search)
            || subjectId is not null
            || !CourseCacheKeys.IsDefaultCatalogSort(sort)
            || !CourseCacheKeys.IsDefaultPage(page, perPage))
        {
            return await inner.ListCourseCatalogAsync(search, subjectId, sort, page, perPage, cancellationToken);
        }

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.StudentCatalog(scope),
            CourseCacheKeys.CatalogPolicy,
            [CourseCacheKeys.CatalogTag(scope)],
            token => inner.ListCourseCatalogAsync(search, subjectId, sort, page, perPage, token),
            cancellationToken);
    }

    public async Task<string?> GetCourseStatusAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        // Deliberately uncached: a cheap single-column read used by the view pre-request for
        // existence/visibility masking. Serving it stale would widen the masking window.
        return await inner.GetCourseStatusAsync(courseId, cancellationToken);
    }

    public async Task<CourseSummaryViewDto?> GetCourseSummaryForViewAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled)
            return await inner.GetCourseSummaryForViewAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.CoursePublicView(scope, courseId),
            CourseCacheKeys.CourseViewPolicy,
            [CourseCacheKeys.CourseTag(scope, courseId), CourseCacheKeys.CourseViewsTag(scope)],
            token => inner.GetCourseSummaryForViewAsync(courseId, token),
            cancellationToken);
    }

    public async Task<CourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled)
            return await inner.GetCourseByIdAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.CourseView(scope, courseId),
            CourseCacheKeys.CourseViewPolicy,
            [CourseCacheKeys.CourseTag(scope, courseId), CourseCacheKeys.CourseViewsTag(scope)],
            token => inner.GetCourseByIdAsync(courseId, token),
            cancellationToken);
    }

    public async Task<CourseReadiness?> GetCourseReadinessAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled)
            return await inner.GetCourseReadinessAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.CourseReadiness(scope, courseId),
            CourseCacheKeys.CourseReadinessPolicy,
            [CourseCacheKeys.CourseTag(scope, courseId), CourseCacheKeys.CourseAssetsTag(scope)],
            token => inner.GetCourseReadinessAsync(courseId, token),
            cancellationToken);
    }

    public async Task<CourseOverviewDto?> GetCourseOverviewAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var scope = CacheScope.ForTenant(tenantProvider.GetTenant());
        if (!scope.IsEnabled)
            return await inner.GetCourseOverviewAsync(courseId, cancellationToken);

        return await cache.GetOrCreateAsync(
            CourseCacheKeys.CourseOverview(scope, courseId),
            CourseCacheKeys.CourseOverviewPolicy,
            [CourseCacheKeys.CourseTag(scope, courseId)],
            token => inner.GetCourseOverviewAsync(courseId, token),
            cancellationToken);
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
