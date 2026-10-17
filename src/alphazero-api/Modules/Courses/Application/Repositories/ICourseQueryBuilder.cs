using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseQueryBuilder
{
    ICourseQueryBuilder ForCourse(Guid courseId);
    ICourseQueryBuilder WithSections();
    ICourseQueryBuilder WithSection(Guid sectionId);
    ICourseQueryBuilder WithItems(bool include = true);
    ICourseQueryBuilder WithResources(bool include = true);
    ICourseQueryBuilder WithPlans(bool include = true);
    ICourseQueryBuilder AsSplitQuery();
    ICourseQueryBuilder IgnoreQueryFilters();
    ICourseQueryBuilder AsNoTracking();

    Task<Course?> ExecuteAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
