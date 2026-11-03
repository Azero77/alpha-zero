using System.Data;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Analytics;

public interface ICourseAnalyticsRefreshService
{
    Task<int> RefreshAnalyticsAsync(CancellationToken cancellationToken);
}

public class CourseAnalyticsRefreshService(
    AppDbContext context,
    ILogger<CourseAnalyticsRefreshService> logger) : ICourseAnalyticsRefreshService
{
    public const string IntervalSecondsConfigKey = "Analytics:RefreshIntervalSeconds";

    public static int ResolveIntervalSeconds(IConfiguration configuration)
    {
        if (int.TryParse(configuration[IntervalSecondsConfigKey], out var parsed) && parsed > 0)
            return parsed;
        return 86400; // 24 hours (daily default per §3.6 / R3-Q5)
    }

    // §3.6 derived-from-source SQL: single set-based UPSERT aggregating all non-expired
    // enrollments (Status != 'Expired' — note R3-Q5: counts every enrollment except expired;
    // 'Expired' is the SQL literal of EnrollmentStatus.Expired — keep them in sync).
    // Table name "Enrollements" is the schema-pinned legacy spelling (#31) — do not fix.
    // ItemCompletions semantics (deliberate, per review): bit positions are bounded by the
    // course's CURRENT active mask, so completions of later-deactivated items drop out on
    // the next refresh — derived-from-source by design (docs/specs/enrollment-lifecycle-and-access-tickets.md §5).
    private const string RefreshSql = """
        WITH enrollment_items AS (
            SELECT
                e."CourseId",
                e."Id" AS "EnrollmentId",
                c."ActiveItemsBitmask",
                gs.i AS bit_index,
                CASE
                    WHEN gs.i < length(e."ProgressBitmask")
                         AND get_bit(e."ProgressBitmask", gs.i) = 1
                         AND get_bit(c."ActiveItemsBitmask", gs.i) = 1
                    THEN 1 ELSE 0
                END AS is_completed_active,
                CASE
                    WHEN gs.i < length(e."ProgressBitmask")
                         AND get_bit(e."ProgressBitmask", gs.i) = 1
                    THEN 1 ELSE 0
                END AS is_item_completed
            FROM "Courses"."Courses" c
            JOIN "Courses"."Enrollements" e ON e."CourseId" = c."Id"
            CROSS JOIN LATERAL generate_series(0, GREATEST(length(c."ActiveItemsBitmask") - 1, 0)) AS gs(i)
            WHERE e."Status" != 'Expired'
              AND length(c."ActiveItemsBitmask") > 0
        ),
        student_progress AS (
            SELECT
                "CourseId",
                "EnrollmentId",
                length(replace("ActiveItemsBitmask"::text, '0', '')) AS active_count,
                SUM(is_completed_active) AS completed_active_count
            FROM enrollment_items
            GROUP BY "CourseId", "EnrollmentId", "ActiveItemsBitmask"
        ),
        student_percentages AS (
            SELECT
                "CourseId",
                "EnrollmentId",
                CASE
                    WHEN active_count > 0
                    THEN LEAST(100.0, (completed_active_count::double precision / active_count::double precision) * 100.0)
                    ELSE 0.0
                END AS completion_percentage
            FROM student_progress
        ),
        course_totals AS (
            SELECT
                c."Id" AS "CourseId",
                COUNT(e."Id") AS total_enrollments,
                COALESCE(SUM(sp.completion_percentage), 0.0) AS sum_of_completion_percentages
            FROM "Courses"."Courses" c
            LEFT JOIN "Courses"."Enrollements" e ON e."CourseId" = c."Id" AND e."Status" != 'Expired'
            LEFT JOIN student_percentages sp ON sp."EnrollmentId" = e."Id"
            GROUP BY c."Id"
        ),
        item_completions AS (
            SELECT
                "CourseId",
                jsonb_object_agg(bit_index::text, bit_count) AS item_completions_json
            FROM (
                SELECT
                    "CourseId",
                    bit_index,
                    SUM(is_item_completed) AS bit_count
                FROM enrollment_items
                GROUP BY "CourseId", bit_index
                HAVING SUM(is_item_completed) > 0
            ) sub
            GROUP BY "CourseId"
        )
        INSERT INTO "Courses"."CourseAnalytics" ("CourseId", "TotalEnrollments", "SumOfCompletionPercentages", "ItemCompletions")
        SELECT
            ct."CourseId",
            ct.total_enrollments,
            ct.sum_of_completion_percentages,
            COALESCE(ic.item_completions_json, '{}'::jsonb)
        FROM course_totals ct
        LEFT JOIN item_completions ic ON ic."CourseId" = ct."CourseId"
        ON CONFLICT ("CourseId") DO UPDATE SET
            "TotalEnrollments" = EXCLUDED."TotalEnrollments",
            "SumOfCompletionPercentages" = EXCLUDED."SumOfCompletionPercentages",
            "ItemCompletions" = EXCLUDED."ItemCompletions";
        """;

    public async Task<int> RefreshAnalyticsAsync(CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = context.Database.GetDbConnection();

            await using var command = connection.CreateCommand();
            if (context.Database.CurrentTransaction is not null)
            {
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            }

            command.CommandText = RefreshSql;

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);

            logger.LogInformation("Refreshed derived course analytics for all courses ({AffectedRows} rows affected).", affected);

            return affected;
        });
    }
}
