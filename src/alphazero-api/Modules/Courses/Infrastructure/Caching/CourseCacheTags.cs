using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// THE one fan-out tag builder for Courses (spec-33 G4.1): the tenant-wide course-view family.
/// Tags exist only for non-enumerable fan-out — a subject rename is not attributable to any
/// single course, and public course-view summaries embed the subject name. It is therefore
/// carried by exactly the public-view read entry, and the subject write decorator is the only
/// place in Courses that queues it. Never add a second Courses tag family; a new "can't
/// enumerate" case returns to grilling instead.
/// </summary>
internal static class CourseCacheTags
{
    /// <summary>Tenant-wide family for public course view entries (name unchanged from #32).</summary>
    public static CacheTag CoursePublicViews(CacheScope scope) => scope.Tag("courses:views");
}
