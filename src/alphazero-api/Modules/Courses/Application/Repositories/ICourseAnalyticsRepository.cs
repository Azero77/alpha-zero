namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseAnalyticsRepository
{
    Task IncrementItemCompletionAsync(Guid courseId, int bitIndex, double diff, CancellationToken cancellationToken = default);
    Task IncrementEnrollmentCountAsync(Guid courseId, CancellationToken cancellationToken = default);
}
