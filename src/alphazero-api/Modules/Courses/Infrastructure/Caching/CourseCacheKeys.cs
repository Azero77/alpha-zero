using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Single registry of Courses cache key shapes, tag families and per-surface TTLs.
///
/// Every key and tag is built by <see cref="CacheScope"/>, so a tenant segment is always
/// present and reads without a tenant bypass the cache entirely. Only default-shaped queries
/// are cached (see <see cref="IsDefaultPage"/> and <see cref="IsDefaultCatalogSort"/>): caller
/// paging, search and sort values never enter a key.
/// </summary>
internal static class CourseCacheKeys
{
    public const int DefaultPage = 1;
    public const int DefaultPerPage = 10;

    public static CachePolicy CatalogPolicy { get; } = new(TimeSpan.FromMinutes(2));
    public static CachePolicy CourseViewPolicy { get; } = new(TimeSpan.FromMinutes(10));
    public static CachePolicy CourseOverviewPolicy { get; } = new(TimeSpan.FromMinutes(10));
    public static CachePolicy CourseReadinessPolicy { get; } = new(TimeSpan.FromMinutes(3));
    public static CachePolicy SubjectListPolicy { get; } = new(TimeSpan.FromMinutes(15));

    // ── Keys ──

    public static CacheKey StaffCatalog(CacheScope scope) =>
        scope.Key("courses:catalog", "class=staff");

    /// <summary>Student catalog (#32): same family and tag as the admin list, distinct class.</summary>
    public static CacheKey StudentCatalog(CacheScope scope) =>
        scope.Key("courses:catalog", "class=student");

    public static CacheKey CourseView(CacheScope scope, Guid courseId) =>
        scope.Key("courses:view", courseId.ToString("N"), "class=staff");

    /// <summary>Public/student course summary (#32's <c>GET /courses/{id}/view</c> shape). The
    /// summary carries no enrollment state — the per-caller overlay is resolved outside this
    /// query — so one entry serves every caller, under its own visibility class.</summary>
    public static CacheKey CoursePublicView(CacheScope scope, Guid courseId) =>
        scope.Key("courses:view", courseId.ToString("N"), "class=public");

    public static CacheKey CourseOverview(CacheScope scope, Guid courseId) =>
        scope.Key("courses:overview", courseId.ToString("N"));

    public static CacheKey CourseReadiness(CacheScope scope, Guid courseId) =>
        scope.Key("courses:readiness", courseId.ToString("N"));

    public static CacheKey SubjectList(CacheScope scope) =>
        scope.Key("courses:subjects");

    // ── Tag families ──

    public static CacheTag CatalogTag(CacheScope scope) => scope.Tag("courses:catalog");

    public static CacheTag SubjectsTag(CacheScope scope) => scope.Tag("courses:subjects");

    /// <summary>Tenant-wide family for course view entries: a subject rename is not attributable
    /// to any single course, and view summaries embed the subject name.</summary>
    public static CacheTag CourseViewsTag(CacheScope scope) => scope.Tag("courses:views");

    /// <summary>Per-course family: tree view, overview and readiness are evicted together.</summary>
    public static CacheTag CourseTag(CacheScope scope, Guid courseId) =>
        scope.Tag("courses:course", courseId.ToString("N"));

    /// <summary>Tenant-wide asset family used as a coarse fallback when an asset's course cannot
    /// be resolved; normal asset eviction targets the per-course tag instead.</summary>
    public static CacheTag CourseAssetsTag(CacheScope scope) => scope.Tag("courses:assets");

    // ── Default-shape predicates ──

    public static bool IsDefaultPage(int page, int perPage) =>
        page == DefaultPage && perPage == DefaultPerPage;

    public static bool IsDefaultCatalogSort(string? sort) =>
        string.IsNullOrWhiteSpace(sort)
        || string.Equals(sort.Trim(), CatalogSortKeys.Newest, StringComparison.OrdinalIgnoreCase);
}
