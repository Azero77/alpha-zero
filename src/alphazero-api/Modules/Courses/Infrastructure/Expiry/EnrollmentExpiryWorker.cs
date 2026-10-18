using AlphaZero.Modules.Courses.Application;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Expiry;

/// <summary>
/// In-process host for the expiry scan (issue #34, addendum: "scheduler/jitter for expiry:
/// investigate module precedent — no new cron/lambda deployment artifact"). The module's only
/// hosted-service precedent is the MassTransit outbox delivery machinery run inside the API host,
/// so expiry follows the same pattern: a small periodic probe that delegates all domain logic to
/// <see cref="EnrollmentExpiryService"/>, resolved through the module's own container per pass
/// (the host-root IUnitOfWork belongs to whichever module registered it globally first).
/// Interval: <c>Enrollment:ExpiryIntervalSeconds</c> (default 3600).
/// </summary>
public class EnrollmentExpiryWorker(
    ICoursesModule coursesModule,
    IServiceScopeFactory scopeFactory,
    ILogger<EnrollmentExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(EnrollmentExpiryService.ResolveIntervalSeconds(
            scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IConfiguration>()));

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
                logger.LogError(ex, "Enrollment expiry scan failed; retrying on the next interval.");
            }
        }
    }
}
