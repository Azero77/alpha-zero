using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Expiry;

/// <summary>
/// One expiry scan pass (issue #34 stories #5/#6): loads a batch of enrollments whose plan access
/// window has passed and drives them through the aggregate's guarded MarkExpired so the expiry
/// domain event rides the module's normal domain-event → outbox → integration-event path.
/// Tenant query filter is bypassed for the scan (enrollments of all tenants age equally);
/// entity payloads carry their own TenantId.
/// </summary>
public interface IEnrollmentExpiryService
{
    Task<int> ExpireDueEnrollmentsAsync(CancellationToken cancellationToken);
}

public class EnrollmentExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<EnrollmentExpiryService> logger) : IEnrollmentExpiryService
{
    public const int BatchSize = 200;
    public const string IntervalSecondsConfigKey = "Enrollment:ExpiryIntervalSeconds";

    public static int ResolveIntervalSeconds(IConfiguration configuration)
    {
        if (int.TryParse(configuration[IntervalSecondsConfigKey], out var parsed) && parsed > 0)
            return parsed;
        return 3600;
    }

    public async Task<int> ExpireDueEnrollmentsAsync(CancellationToken cancellationToken)
    {
        var expiredCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var context = scope.ServiceProvider.GetRequiredService<Persistance.AppDbContext>();

            var batch = await LoadDueBatchAsync(context, cancellationToken);
            if (batch.Count == 0)
                break;

            foreach (var enrollment in batch)
            {
                var result = enrollment.MarkExpired(DateTime.UtcNow);
                if (result.IsError)
                {
                    logger.LogWarning("Skipping enrollment {EnrollmentId} during expiry: {Errors}",
                        enrollment.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                    continue;
                }
                unitOfWork.TrackEntity(enrollment);
                expiredCount++;
            }

            // UnitOfWork publishes the domain events (→ integration events via the module's
            // event handler) and persists the transition in one step.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (batch.Count < BatchSize)
                break;
        }

        if (expiredCount > 0)
            logger.LogInformation("Enrollment expiry pass marked {Count} enrollment(s) as Expired.", expiredCount);

        return expiredCount;
    }

    internal static async Task<List<Enrollment>> LoadDueBatchAsync(
        Persistance.AppDbContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return await context.Enrollments
            .IgnoreQueryFilters([DbContstants.TenantFilter])
            .Where(e => e.AccessExpiresOn != null && e.AccessExpiresOn <= now)
            .Where(e => e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Suspended)
            .OrderBy(e => e.AccessExpiresOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }
}
