using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Infrastructure.Analytics;

/// <summary>
/// Scheduled job (issue #59, Courses §3.6 template): on each pass it first sweeps
/// expired access codes through the state machine (guarded path) and then refreshes the
/// <c>LibraryAnalytics</c> projection with one set-based UPSERT, so expiry is reflected in the
/// funnel in the same pass. Configurable via <c>LibraryAnalytics:RefreshIntervalSeconds</c>
/// (default: daily). Runs the first pass immediately at startup (run-then-delay) so a fresh
/// database is not without analytics rows for a full interval.
/// The sweep/refresh services are resolved through <c>ILibraryModule.CreateScope()</c> so their
/// <c>IUnitOfWork</c>/<c>AppDbContext</c> unambiguously belong to the Library module's container.
/// </summary>
public class LibraryAnalyticsRefreshWorker(
    AlphaZero.Modules.Library.Application.ILibraryModule libraryModule,
    IConfiguration configuration,
    ILogger<LibraryAnalyticsRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            LibraryAnalyticsRefreshService.ResolveIntervalSeconds(configuration));

        logger.LogInformation("Library analytics worker started (scheduled every {Interval}).", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var moduleScope = libraryModule.CreateScope();

                // Sweep first (state), refresh second (projection) so Expired transitions land
                // in the funnel on the same pass.
                var sweepService = moduleScope.Resolve<IAccessCodeExpirySweepService>();
                await sweepService.SweepExpiredCodesAsync(stoppingToken);

                var refreshService = moduleScope.Resolve<ILibraryAnalyticsRefreshService>();
                await refreshService.RefreshAnalyticsAsync(stoppingToken);

                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // host is shutting down
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Library analytics worker pass failed; retrying on next interval.");
                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
