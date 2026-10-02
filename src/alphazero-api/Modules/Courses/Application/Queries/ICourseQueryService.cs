using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface ICourseQueryService
{
    Task<CourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default);
    Task<PagedResult<CourseSummaryDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken cancellationToken = default);
}
