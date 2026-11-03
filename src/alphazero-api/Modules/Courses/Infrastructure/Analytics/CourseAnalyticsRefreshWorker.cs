using AlphaZero.Modules.Courses.Application;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Analytics;

/// <summary>
/// Scheduled daily job (issue #34 rework §3.6, step 12): refreshes derived-from-source analytics
/// for all courses using a single set-based SQL statement. Configurable interval
/// via <c>Analytics:RefreshIntervalSeconds</c> (defaults to the service's daily constant).
/// Runs the first refresh immediately at startup so a fresh database isn't
/// without analytics rows for a full interval (review fix: run-then-delay).
/// </summary>
public class CourseAnalyticsRefreshWorker(
    ICoursesModule coursesModule,
    IConfiguration configuration,
    ILogger<CourseAnalyticsRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            CourseAnalyticsRefreshService.ResolveIntervalSeconds(configuration));

        logger.LogInformation("Course analytics refresh worker started (scheduled every {Interval}).", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var moduleScope = coursesModule.CreateScope();
                var refreshService = moduleScope.Resolve<ICourseAnalyticsRefreshService>();
                await refreshService.RefreshAnalyticsAsync(stoppingToken);

                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // host is shutting down
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Course analytics refresh job failed; retrying on next interval.");
            }
        }
    }
}
