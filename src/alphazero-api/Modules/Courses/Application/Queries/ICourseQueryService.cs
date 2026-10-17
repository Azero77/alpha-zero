using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface ICourseQueryService
{
    Task<CourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<PagedResult<CourseSummaryDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken cancellationToken = default);
    Task<CourseReadiness?> GetCourseReadinessAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<CourseOverviewDto?> GetCourseOverviewAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<List<CourseAssetDto>> GetCoursePoolAsync(Guid courseId, bool includeAssigned = false, CancellationToken cancellationToken = default);
}
