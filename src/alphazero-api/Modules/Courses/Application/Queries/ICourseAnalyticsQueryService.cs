using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface ICourseAnalyticsQueryService
{
    Task<CourseAnalyticsDto?> GetCourseAnalyticsAsync(Guid courseId, CancellationToken cancellationToken = default);
}
