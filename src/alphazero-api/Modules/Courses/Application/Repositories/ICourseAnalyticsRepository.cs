namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseAnalyticsRepository
{
    Task IncrementItemCompletionAsync(Guid courseId, int bitIndex, double diff, CancellationToken cancellationToken = default);
    Task IncrementEnrollmentCountAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an expired enrollment's contribution from the running course analytics
    /// (issue #34 story #6 — "finished and gone" no longer pulls active-course averages) and
    /// clamps at zero so repeated scans cannot drive counters negative.
    /// </summary>
    Task DecrementExpiredEnrollmentAsync(Guid courseId, double completionPercentage, IReadOnlyCollection<int> completedItemIndexes, CancellationToken cancellationToken = default);
}
