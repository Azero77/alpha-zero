using System.Data;
using System.Data.Common;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Expiry;

/// <summary>
/// Set-based expiry sweep (issue #34 rework §3.4(c), step 11): replaces the legacy per-row aggregate
/// scan with a single atomic set-based PostgreSQL UPDATE that transitions due Active enrollments to
/// Expired, inserts the corresponding audit rows in EnrollmentAuditEntries in the same statement,
/// and bulk-publishes EnrollmentExpiredIntegrationEvents from the RETURNING projection.
///
/// Aggregate discipline is intentionally short-circuited here by explicit architectural decision
/// (§3.4(c)): no aggregate loads, no per-row analytics writes, zero N+1 round trips.
/// </summary>
public interface IEnrollmentExpiryService
{
    Task<int> ExpireDueEnrollmentsAsync(CancellationToken cancellationToken);
}

public class EnrollmentExpiryService(
    AppDbContext context,
    IPublishEndpoint publishEndpoint,
    ILogger<EnrollmentExpiryService> logger) : IEnrollmentExpiryService
{
    public const string IntervalSecondsConfigKey = "Enrollment:ExpiryIntervalSeconds";

    public static int ResolveIntervalSeconds(IConfiguration configuration)
    {
        if (int.TryParse(configuration[IntervalSecondsConfigKey], out var parsed) && parsed > 0)
            return parsed;
        return 3600;
    }

    private const string SweepSql = """
        WITH updated AS (
            UPDATE "Courses"."Enrollements"
            SET "Status" = 'Expired'
            WHERE "Status" = 'Active'
              AND "AccessExpiresOn" IS NOT NULL
              AND "AccessExpiresOn" <= @now
            RETURNING "Id", "TenantId", "StudentId", "CourseId"
        ),
        audited AS (
            INSERT INTO "Courses"."EnrollmentAuditEntries"
                ("Id", "TenantId", "SubjectId", "EventKind", "ActorId", "OccurredOn", "Metadata")
            SELECT
                gen_random_uuid(),
                u."TenantId",
                u."Id",
                'Expired',
                NULL,
                @now,
                '{"From":"Active","To":"Expired"}'::jsonb
            FROM updated u
        )
        SELECT u."Id", u."TenantId", u."StudentId", u."CourseId"
        FROM updated u;
        """;

    public async Task<int> ExpireDueEnrollmentsAsync(CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var now = DateTime.UtcNow;

            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = context.Database.GetDbConnection();

            await using var command = connection.CreateCommand();
            if (context.Database.CurrentTransaction is not null)
            {
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            }

            command.CommandText = SweepSql;

            var nowParam = command.CreateParameter();
            nowParam.ParameterName = "now";
            nowParam.Value = now;
            command.Parameters.Add(nowParam);

            var expired = new List<ExpiredEnrollmentRow>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                var idOrd = reader.GetOrdinal("Id");
                var tenantOrd = reader.GetOrdinal("TenantId");
                var studentOrd = reader.GetOrdinal("StudentId");
                var courseOrd = reader.GetOrdinal("CourseId");

                while (await reader.ReadAsync(cancellationToken))
                {
                    expired.Add(new ExpiredEnrollmentRow(
                        reader.GetGuid(idOrd),
                        reader.GetGuid(tenantOrd),
                        reader.GetGuid(studentOrd),
                        reader.GetGuid(courseOrd)));
                }
            }

            if (expired.Count == 0)
            {
                return 0;
            }

            foreach (var row in expired)
            {
                await publishEndpoint.Publish(new EnrollmentExpiredIntegrationEvent(
                    row.Id,
                    row.StudentId,
                    row.CourseId,
                    row.TenantId), cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Enrollment expiry sweep marked {Count} enrollment(s) as Expired in a single set-based pass.",
                expired.Count);

            return expired.Count;
        });
    }

    private sealed record ExpiredEnrollmentRow(Guid Id, Guid TenantId, Guid StudentId, Guid CourseId);
}
