using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// The Courses module's single eviction policy: maps every persisted entity that feeds a cached
/// projection to its cache tags. Dispatch is on the EF metadata CLR type (concrete TPH types),
/// and parent course/tenant identity is resolved from the change tracker's data — never from
/// command types — so any command that persists these entities evicts correctly.
///
/// Over-eviction is deliberate: a missing tag is a correctness bug, an extra one is a cache miss.
/// </summary>
public sealed class CourseEvictionPolicy : IEntityEvictionPolicy
{
    private static readonly HashSet<Type> DirectTypes =
    [
        typeof(Course),
        typeof(CourseSection),
        typeof(CoursePlan),
        typeof(CurriculumItem),
        typeof(CourseOverview),
        typeof(SubjectAggregate)
    ];

    public bool Handles(Type entityType) =>
        DirectTypes.Contains(entityType)
        || typeof(CourseAsset).IsAssignableFrom(entityType)
        || typeof(CurriculumResource).IsAssignableFrom(entityType);

    public async Task<IReadOnlyCollection<CacheTag>> ResolveTagsAsync(
        EntityEntry entry,
        DbContext context,
        CancellationToken cancellationToken)
    {
        var lookup = new Lookup(context);

        return entry.Entity switch
        {
            Course course => ForCourse(course.TenantId, course.Id),
            CourseSection section => await ForCourseIdAsync(section.CourseId, lookup, cancellationToken),
            CoursePlan plan => await ForCourseIdAsync(plan.CourseId, lookup, cancellationToken),
            CurriculumItem item => await ForItemAsync(item.SectionId, lookup, cancellationToken),
            CurriculumResource resource => await ForResourceAsync(resource.CurriculumItemId, lookup, cancellationToken),
            CourseOverview overview => ForCourse(overview.TenantId, overview.CourseId),
            CourseAsset asset => ForAsset(asset),
            SubjectAggregate subject => ForSubject(subject.TenantId),
            _ => []
        };
    }

    private static IReadOnlyCollection<CacheTag> ForCourse(Guid tenantId, Guid courseId)
    {
        if (tenantId == Guid.Empty || courseId == Guid.Empty)
            return [];

        var scope = CacheScope.ForTenant(tenantId);
        return [CourseCacheKeys.CourseTag(scope, courseId), CourseCacheKeys.CatalogTag(scope)];
    }

    private static IReadOnlyCollection<CacheTag> ForAsset(CourseAsset asset)
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

    private static IReadOnlyCollection<CacheTag> ForSubject(Guid tenantId)
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

    private static async Task<IReadOnlyCollection<CacheTag>> ForCourseIdAsync(
        Guid courseId,
        Lookup lookup,
        CancellationToken cancellationToken)
    {
        var course = await lookup.ResolveCourseAsync(courseId, cancellationToken);
        return course is null ? [] : ForCourse(course.Value.TenantId, course.Value.CourseId);
    }

    private static async Task<IReadOnlyCollection<CacheTag>> ForItemAsync(
        Guid sectionId,
        Lookup lookup,
        CancellationToken cancellationToken)
    {
        var courseId = await lookup.ResolveCourseIdFromSectionAsync(sectionId, cancellationToken);
        return courseId is null ? [] : await ForCourseIdAsync(courseId.Value, lookup, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<CacheTag>> ForResourceAsync(
        Guid itemId,
        Lookup lookup,
        CancellationToken cancellationToken)
    {
        var sectionId = await lookup.ResolveSectionIdFromItemAsync(itemId, cancellationToken);
        return sectionId is null ? [] : await ForItemAsync(sectionId.Value, lookup, cancellationToken);
    }

    private readonly record struct CourseRef(Guid CourseId, Guid TenantId);

    /// <summary>
    /// Parent resolution runs before the write, so deleted rows still exist; query filters are
    /// bypassed so a soft-deleted or cross-filtered parent still yields its tenant and course.
    /// </summary>
    private sealed class Lookup(DbContext context)
    {
        private readonly Dictionary<Guid, CourseRef?> _courses = new();
        private readonly Dictionary<Guid, Guid?> _sections = new();
        private readonly Dictionary<Guid, Guid?> _items = new();

        public async Task<CourseRef?> ResolveCourseAsync(Guid courseId, CancellationToken cancellationToken)
        {
            if (courseId == Guid.Empty)
                return null;

            if (_courses.TryGetValue(courseId, out var cached))
                return cached;

            var course = await context.Set<Course>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == courseId)
                .Select(c => new CourseRef(c.Id, c.TenantId))
                .FirstOrDefaultAsync(cancellationToken);

            var result = course == default ? (CourseRef?)null : course;
            _courses[courseId] = result;
            return result;
        }

        public async Task<Guid?> ResolveCourseIdFromSectionAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            if (sectionId == Guid.Empty)
                return null;

            if (_sections.TryGetValue(sectionId, out var cached))
                return cached;

            var courseId = await context.Set<CourseSection>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.Id == sectionId)
                .Select(s => (Guid?)s.CourseId)
                .FirstOrDefaultAsync(cancellationToken);

            _sections[sectionId] = courseId;
            return courseId;
        }

        public async Task<Guid?> ResolveSectionIdFromItemAsync(Guid itemId, CancellationToken cancellationToken)
        {
            if (itemId == Guid.Empty)
                return null;

            if (_items.TryGetValue(itemId, out var cached))
                return cached;

            var sectionId = await context.Set<CurriculumItem>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(i => i.Id == itemId)
                .Select(i => (Guid?)i.SectionId)
                .FirstOrDefaultAsync(cancellationToken);

            _items[itemId] = sectionId;
            return sectionId;
        }
    }
}
