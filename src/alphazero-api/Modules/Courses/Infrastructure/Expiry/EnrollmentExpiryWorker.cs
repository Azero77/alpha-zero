using AlphaZero.Modules.Courses.Application;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Expiry;

/// <summary>
/// In-process host for the set-based expiry sweep (issue #34 rework §3.4(c), step 11).
/// Resolves <see cref="IEnrollmentExpiryService"/> through the module's own container per pass.
/// Interval: <c>Enrollment:ExpiryIntervalSeconds</c> (default 3600).
/// Hardening: no leaked configuration scope, single outer scope per pass.
/// </summary>
public class EnrollmentExpiryWorker(
    ICoursesModule coursesModule,
    IConfiguration configuration,
    ILogger<EnrollmentExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            EnrollmentExpiryService.ResolveIntervalSeconds(configuration));

        logger.LogInformation("Enrollment expiry worker started (scan every {Interval}).", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var moduleScope = coursesModule.CreateScope();
                var expiryService = moduleScope.Resolve<IEnrollmentExpiryService>();
                await expiryService.ExpireDueEnrollmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // host is shutting down
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Enrollment expiry sweep failed; retrying on the next interval.");
            }
        }
    }
}
