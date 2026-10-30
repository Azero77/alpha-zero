using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Single registry of Courses cache key shapes, key-set helpers and per-surface TTLs.
///
/// Every key is built by <see cref="CacheScope"/>, so a tenant segment is always present and
/// reads without a tenant bypass the cache entirely. Only default-shaped queries are cached
/// (see <see cref="IsDefaultPage"/> and <see cref="IsDefaultCatalogSort"/>): caller paging,
/// search and sort values never enter a key. The one fan-out tag lives in
/// <see cref="CourseCacheTags"/>; tags are never registered from here.
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

    /// <summary>Read-through TTL for single-entity repository reads (spec-33 §2 tree).</summary>
    public static CachePolicy EntityPolicy { get; } = new(TimeSpan.FromMinutes(5));

    // ── Keys ──

    public static CacheKey StaffCatalog(CacheScope scope) =>
        scope.Key("courses:catalog", "class=staff");

    /// <summary>Student catalog (#32): same family and tag as the admin list, distinct class.</summary>
    public static CacheKey StudentCatalog(CacheScope scope) =>
        scope.Key("courses:catalog", "class=student");

    /// <summary>Staff course view / tree projection (sections with items).</summary>
    public static CacheKey CourseView(CacheScope scope, Guid courseId) =>
        scope.Key("courses:view", courseId.ToString("N"), "class=staff");

    /// <summary>Public/student course summary (#32's <c>GET /courses/{id}/view</c> shape). The
    /// summary carries no enrollment state — the per-caller overlay is resolved outside this
    /// query — so one entry serves every caller, under its own visibility class. This family is
    /// the one tagged with the fan-out tag (see <see cref="CourseCacheTags"/>).</summary>
    public static CacheKey CoursePublicView(CacheScope scope, Guid courseId) =>
        scope.Key("courses:view", courseId.ToString("N"), "class=public");

    public static CacheKey CourseOverview(CacheScope scope, Guid courseId) =>
        scope.Key("courses:overview", courseId.ToString("N"));

    public static CacheKey CourseReadiness(CacheScope scope, Guid courseId) =>
        scope.Key("courses:readiness", courseId.ToString("N"));

    /// <summary>Eviction-only status key: the status read is deliberately uncached today (a
    /// cheap single-column read that must not widen visibility masking); queuing the key is a
    /// defensive no-op until that ever changes.</summary>
    public static CacheKey CourseStatus(CacheScope scope, Guid courseId) =>
        scope.Key("courses:status", courseId.ToString("N"));

    public static CacheKey SubjectList(CacheScope scope) =>
        scope.Key("courses:subjects");

    // ── Read-through entity keys (decorator single-entity reads) ──

    /// <summary>Course aggregate read-through key. Today aggregate <c>GetById</c> reads delegate
    /// UNCACHED (the aggregate is a tracked-identity bridge), so this key serves eviction only.</summary>
    public static CacheKey CourseEntity(CacheScope scope, Guid courseId) =>
        scope.Key("entity:course", courseId.ToString("N"));

    /// <summary>Overview read-through key for <c>GetByCourseIdAsync</c> (a flat, STJ-safe DTO).</summary>
    public static CacheKey CourseOverviewEntity(CacheScope scope, Guid courseId) =>
        scope.Key("entity:overview", courseId.ToString("N"));

    /// <summary>Asset read-through key; eviction only today (asset reads are write-preceding loads).</summary>
    public static CacheKey CourseAssetEntity(CacheScope scope, Guid assetId) =>
        scope.Key("entity:asset", assetId.ToString("N"));

    /// <summary>Subject read-through key; eviction only today (subject reads are write-preceding loads).</summary>
    public static CacheKey SubjectEntity(CacheScope scope, Guid subjectId) =>
        scope.Key("entity:subject", subjectId.ToString("N"));

    // ── Key-set helpers (one per eviction-matrix write row) ──
    //
    // Every helper derives its keys from the aggregate argument only — no lookups, no EF
    // change-tracker reads — and includes the matching *Entity key so read-through entries can
    // never outlive a write to their source. Over-eviction (a key removed although never present)
    // is always a safe no-op; under-eviction is a bug.

    /// <summary>Course (any root/section/item/resource/plan/reorder/status/soft-delete) → the
    /// 8-key row of the eviction matrix: catalog:staff, catalog:student, tree (staff view),
    /// view:public, overview, readiness, status, entity:course.</summary>
    public static IReadOnlyCollection<CacheKey> EvictCourse(CacheScope scope, Guid courseId) =>
    [
        StaffCatalog(scope),
        StudentCatalog(scope),
        CourseView(scope, courseId),
        CoursePublicView(scope, courseId),
        CourseOverview(scope, courseId),
        CourseReadiness(scope, courseId),
        CourseStatus(scope, courseId),
        CourseEntity(scope, courseId)
    ];

    /// <summary>CourseOverview upsert → overview, readiness, view:public, catalog:staff,
    /// catalog:student, plus the matching overview read-through key (the upsert handler makes
    /// its add-or-update decision from the cached DTO, so a live copy must never outlive the
    /// write that would change it — e.g. a cover-image change).</summary>
    public static IReadOnlyCollection<CacheKey> EvictCourseOverview(CacheScope scope, Guid courseId) =>
    [
        CourseOverview(scope, courseId),
        CourseReadiness(scope, courseId),
        CoursePublicView(scope, courseId),
        StaffCatalog(scope),
        StudentCatalog(scope),
        CourseOverviewEntity(scope, courseId)
    ];

    /// <summary>CourseAsset (any TPH subtype; state/duration/info) → readiness, view:public,
    /// catalog:staff, catalog:student plus the matching asset read-through key — NOT the tree
    /// (the staff-tree projection reads only <c>r.CourseAssetId</c>, never the asset table).
    /// An unassigned pool asset (<c>CourseId == Guid.Empty</c>) queues NOTHING.</summary>
    public static IReadOnlyCollection<CacheKey> EvictCourseAsset(CacheScope scope, Guid courseId, Guid assetId)
    {
        if (courseId == Guid.Empty)
            return [];

        return
        [
            CourseReadiness(scope, courseId),
            CoursePublicView(scope, courseId),
            StaffCatalog(scope),
            StudentCatalog(scope),
            CourseAssetEntity(scope, assetId)
        ];
    }

    /// <summary>Subject (create/rename/update) → SubjectList + SubjectEntity. The subject
    /// decorator additionally queues the one fan-out tag (see <see cref="CourseCacheTags"/>).</summary>
    public static IReadOnlyCollection<CacheKey> EvictSubject(CacheScope scope, Guid subjectId) =>
    [
        SubjectList(scope),
        SubjectEntity(scope, subjectId)
    ];

    // ── Default-shape predicates ──

    public static bool IsDefaultPage(int page, int perPage) =>
        page == DefaultPage && perPage == DefaultPerPage;

    public static bool IsDefaultCatalogSort(string? sort) =>
        string.IsNullOrWhiteSpace(sort)
        || string.Equals(sort.Trim(), CatalogSortKeys.Newest, StringComparison.OrdinalIgnoreCase);
}
