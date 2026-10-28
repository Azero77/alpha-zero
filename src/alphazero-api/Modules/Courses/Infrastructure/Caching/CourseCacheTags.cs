using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Pure entity-to-tag mapping for Courses writes. Kept in one place so every write decorator
/// emits the same families the cached read surfaces are tagged with.
/// </summary>
internal static class CourseCacheTags
{
    public static IReadOnlyCollection<CacheTag> ForCourse(Guid tenantId, Guid courseId)
    {
        if (tenantId == Guid.Empty || courseId == Guid.Empty)
            return [];

        var scope = CacheScope.ForTenant(tenantId);
        return [CourseCacheKeys.CourseTag(scope, courseId), CourseCacheKeys.CatalogTag(scope)];
    }

    /// <summary>Overview edits never change catalog summary fields, so only the per-course
    /// family is evicted.</summary>
    public static IReadOnlyCollection<CacheTag> ForCourseOverview(Guid tenantId, Guid courseId)
    {
        if (tenantId == Guid.Empty || courseId == Guid.Empty)
            return [];

        return [CourseCacheKeys.CourseTag(CacheScope.ForTenant(tenantId), courseId)];
    }

    public static IReadOnlyCollection<CacheTag> ForAsset(CourseAsset asset)
    {
        if (asset.TenantId == Guid.Empty)
            return [];

        var scope = CacheScope.ForTenant(asset.TenantId);

        // A resolved course is the precise family (readiness entries carry the course tag).
        // The tenant-wide fallback only exists for assets with no course binding.
        return asset.CourseId != Guid.Empty
            ? [CourseCacheKeys.CourseTag(scope, asset.CourseId)]
            : [CourseCacheKeys.CourseAssetsTag(scope)];
    }

    public static IReadOnlyCollection<CacheTag> ForSubject(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return [];

        var scope = CacheScope.ForTenant(tenantId);
        return
        [
            CourseCacheKeys.SubjectsTag(scope),
            CourseCacheKeys.CatalogTag(scope),
            CourseCacheKeys.CourseViewsTag(scope)
        ];
    }
}
