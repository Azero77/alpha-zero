using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Central, single-source registry of the Courses module's cache key shapes, tag families
/// (eviction families) and per-surface TTLs. Requirements encoded here:
///
/// 1. TENANT ISOLATION IS A KEY PROPERTY: every key *and* every tag carries the tenant id
///    component. One academy's cached entries can never resolve into another's (tests assert
///    this behaviorally). Reads without a tenant context bypass the cache entirely (pass-through
///    to the database) — a cache entry never exists without a tenant.
/// 2. NO VISIBILITY-VARIANT KEY SHARING: view-family keys carry an explicit class segment so a
///    future visibility variant (e.g. an enrolled student view) carves its own keys instead of
///    aliasing the current ones.
/// 3. TTLs are the documented caching tolerances (see Modules/Courses/CACHING.md):
///    catalog 2 min, course tree view 10 min, course overview 10 min, readiness companion 3 min,
///    tenant subject listing 15 min.
/// 4. Eviction is TAG-based: mutation commands remove whole tag families (see
///    <see cref="CourseCacheEvictionBehavior{TRequest,TResponse}"/>); individual keys are never
///    hand-listed at the call sites except where an exact single entry must go.
/// </summary>
internal static class CourseCacheKeys
{
    /// <summary>Placeholder for the (rare) no-tenant background context. Reads with a null
    /// tenant never reach these keys — they bypass the cache — this exists only so eviction
    /// code can clamp defensively.</summary>
    private const string NoTenant = "none";

    // ── Keys ──

    public static string CatalogKey(Guid? tenantId, Guid? subjectId, int page, int perPage) =>
        $"courses:catalog:{Tenant(tenantId)}:subject={subjectId?.ToString() ?? "all"}:p={page}:size={perPage}";

    public static string CourseViewKey(Guid? tenantId, Guid courseId) =>
        $"courses:view:{Tenant(tenantId)}:{courseId}:class=staff";

    public static string CourseOverviewKey(Guid? tenantId, Guid courseId) =>
        $"courses:overview:{Tenant(tenantId)}:{courseId}";

    public static string CourseReadinessKey(Guid? tenantId, Guid courseId) =>
        $"courses:readiness:{Tenant(tenantId)}:{courseId}";

    public static string SubjectListKey(Guid? tenantId, int page, int perPage) =>
        $"courses:subjects:{Tenant(tenantId)}:p={page}:size={perPage}";

    // ── Tag families (eviction families) ──

    public static string CatalogTag(Guid? tenantId) => $"courses:catalog:{Tenant(tenantId)}";
    public static string SubjectsTag(Guid? tenantId) => $"courses:subjects:{Tenant(tenantId)}";

    /// <summary>Per-course family: the course tree view, its overview and its readiness all
    /// carry this tag and are evicted together by course-level mutations.</summary>
    public static string CourseTag(Guid? tenantId, Guid courseId) =>
        $"courses:course:{Tenant(tenantId)}:{courseId}";

    /// <summary>Tenant-wide asset family: asset state/metadata changes (from the media pipeline)
    /// affect readiness, but the triggering commands carry no course id — so readiness entries
    /// additionally carry this coarse tenant-level tag and asset commands evict it.</summary>
    public static string CourseAssetsTag(Guid? tenantId) => $"courses:assets:{Tenant(tenantId)}";

    // ── Entry options (per-surface TTL — the documented caching tolerances) ──

    public static HybridCacheEntryOptions CatalogEntry { get; } = Entry(TimeSpan.FromMinutes(2));
    public static HybridCacheEntryOptions CourseViewEntry { get; } = Entry(TimeSpan.FromMinutes(10));
    public static HybridCacheEntryOptions CourseOverviewEntry { get; } = Entry(TimeSpan.FromMinutes(10));
    public static HybridCacheEntryOptions CourseReadinessEntry { get; } = Entry(TimeSpan.FromMinutes(3));
    public static HybridCacheEntryOptions SubjectListEntry { get; } = Entry(TimeSpan.FromMinutes(15));

    private static HybridCacheEntryOptions Entry(TimeSpan expiration) => new()
    {
        Expiration = expiration
    };

    private static string Tenant(Guid? tenantId) => tenantId?.ToString() ?? NoTenant;
}
