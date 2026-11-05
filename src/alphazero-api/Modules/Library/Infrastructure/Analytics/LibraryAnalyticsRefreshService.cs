using AlphaZero.Modules.Library.Infrastructure.Models;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Infrastructure.Analytics;

public interface ILibraryAnalyticsRefreshService
{
    Task<int> RefreshAnalyticsAsync(CancellationToken cancellationToken = default);
}

public class LibraryAnalyticsRefreshService(
    AppDbContext context,
    ILogger<LibraryAnalyticsRefreshService> logger) : ILibraryAnalyticsRefreshService
{
    public const string IntervalSecondsConfigKey = "LibraryAnalytics:RefreshIntervalSeconds";

    public static int ResolveIntervalSeconds(IConfiguration configuration)
    {
        if (int.TryParse(configuration[IntervalSecondsConfigKey], out var parsed) && parsed > 0)
            return parsed;
        return 86400; // 24 hours (daily default, mirroring the Courses analytics worker)
    }

    // Single set-based UPSERT (issue #59, Courses §3.6 template): SQL literals for status below
    // match AccessCodeStatus values as persisted by HasConversion<string>() — keep in sync.
    // Invariant I-MONEY: 'sold' counts and all revenue derive EXCLUSIVELY from SaleRecords rows
    // joined back to their access codes; code status is never used as a money source.
    // VoidedAfterSaleCount is the ratified refund-visibility column: SaleRecord rows whose code is
    // now Voided (sale exists, access recalled — a refund-as-void signal).
    // Sentinel UUID '00000000-0000-0000-0000-000000000000': LibraryId = sentinel denotes codes
    // without an owning library (admin codes); BatchId = sentinel denotes codes without a batch.
    // Sentinels keep the composite key non-nullable for ON CONFLICT (see LibraryAnalytics doc).
    private const string RefreshSql = """
        WITH codes AS (
            SELECT
                a."TenantId",
                COALESCE(a."LibraryId", '00000000-0000-0000-0000-000000000000'::uuid) AS "LibraryId",
                COALESCE(a."BatchId", '00000000-0000-0000-0000-000000000000'::uuid)  AS "BatchId",
                a."Status"
            FROM "Library"."AccessCodes" a
        ),
        funnel AS (
            SELECT
                "TenantId", "LibraryId", "BatchId",
                COUNT(*) AS "MintedCount",
                COUNT(*) FILTER (WHERE "Status" = 'Distributed') AS "DistributedCount",
                COUNT(*) FILTER (WHERE "Status" = 'Redeemed') AS "RedeemedCount",
                COUNT(*) FILTER (WHERE "Status" = 'Voided') AS "VoidedCount",
                COUNT(*) FILTER (WHERE "Status" = 'Expired') AS "ExpiredCount",
                COUNT(*) FILTER (WHERE "Status" IN ('Distributed', 'Sold')) AS "OutstandingCount"
            FROM codes
            GROUP BY "TenantId", "LibraryId", "BatchId"
        ),
        sales AS (
            SELECT
                a."TenantId",
                COALESCE(a."LibraryId", '00000000-0000-0000-0000-000000000000'::uuid) AS "LibraryId",
                COALESCE(a."BatchId", '00000000-0000-0000-0000-000000000000'::uuid)  AS "BatchId",
                s."Price",
                s."Currency",
                s."SoldAt" AT TIME ZONE 'UTC' AS "SoldAtUtc",
                a."Status" AS "CodeStatus"
            FROM "Library"."SaleRecords" s
            JOIN "Library"."AccessCodes" a ON a."Id" = s."AccessCodeId"
        ),
        sales_totals AS (
            SELECT
                "TenantId", "LibraryId", "BatchId",
                COUNT(*) AS "SoldCount",
                COALESCE(SUM("Price"), 0)::numeric(18, 2) AS "TotalRevenue",
                COUNT(*) FILTER (WHERE "CodeStatus" = 'Voided') AS "VoidedAfterSaleCount"
            FROM sales
            GROUP BY "TenantId", "LibraryId", "BatchId"
        ),
        period_sales AS (
            SELECT
                "TenantId", "LibraryId", "BatchId",
                to_char("SoldAtUtc", 'YYYY-MM') AS "Period",
                COUNT(*) AS "SalesCount"
            FROM sales
            GROUP BY "TenantId", "LibraryId", "BatchId", to_char("SoldAtUtc", 'YYYY-MM')
        ),
        period_revenue AS (
            SELECT
                "TenantId", "LibraryId", "BatchId",
                to_char("SoldAtUtc", 'YYYY-MM') AS "Period",
                "Currency",
                SUM("Price") AS "Revenue"
            FROM sales
            GROUP BY "TenantId", "LibraryId", "BatchId", to_char("SoldAtUtc", 'YYYY-MM'), "Currency"
        ),
        revenue_by_period AS (
            SELECT
                pr."TenantId", pr."LibraryId", pr."BatchId",
                jsonb_object_agg(
                    pr."Period",
                    jsonb_object_agg_currency
                ) AS "RevenueByPeriod"
            FROM (
                SELECT
                    "TenantId", "LibraryId", "BatchId", "Period",
                    jsonb_object_agg("Currency", "Revenue") AS jsonb_object_agg_currency
                FROM period_revenue
                GROUP BY "TenantId", "LibraryId", "BatchId", "Period"
            ) pr
            GROUP BY pr."TenantId", pr."LibraryId", pr."BatchId"
        ),
        sales_by_period AS (
            SELECT
                "TenantId", "LibraryId", "BatchId",
                jsonb_object_agg("Period", "SalesCount") AS "SalesByPeriod"
            FROM period_sales
            GROUP BY "TenantId", "LibraryId", "BatchId"
        )
        INSERT INTO "Library"."LibraryAnalytics"
            ("TenantId", "LibraryId", "BatchId",
             "MintedCount", "DistributedCount", "SoldCount", "RedeemedCount", "VoidedCount", "ExpiredCount",
             "OutstandingCount", "VoidedAfterSaleCount", "TotalRevenue", "RevenueByPeriod", "SalesByPeriod")
        SELECT
            f."TenantId",
            f."LibraryId",
            f."BatchId",
            f."MintedCount",
            f."DistributedCount",
            COALESCE(st."SoldCount", 0),
            f."RedeemedCount",
            f."VoidedCount",
            f."ExpiredCount",
            f."OutstandingCount",
            COALESCE(st."VoidedAfterSaleCount", 0),
            COALESCE(st."TotalRevenue", 0)::numeric(18, 2),
            COALESCE(rp."RevenueByPeriod", '{}'::jsonb),
            COALESCE(sp."SalesByPeriod", '{}'::jsonb)
        FROM funnel f
        LEFT JOIN sales_totals st
            ON st."TenantId" = f."TenantId" AND st."LibraryId" = f."LibraryId" AND st."BatchId" = f."BatchId"
        LEFT JOIN revenue_by_period rp
            ON rp."TenantId" = f."TenantId" AND rp."LibraryId" = f."LibraryId" AND rp."BatchId" = f."BatchId"
        LEFT JOIN sales_by_period sp
            ON sp."TenantId" = f."TenantId" AND sp."LibraryId" = f."LibraryId" AND sp."BatchId" = f."BatchId"
        ON CONFLICT ("TenantId", "LibraryId", "BatchId") DO UPDATE SET
            "MintedCount" = EXCLUDED."MintedCount",
            "DistributedCount" = EXCLUDED."DistributedCount",
            "SoldCount" = EXCLUDED."SoldCount",
            "RedeemedCount" = EXCLUDED."RedeemedCount",
            "VoidedCount" = EXCLUDED."VoidedCount",
            "ExpiredCount" = EXCLUDED."ExpiredCount",
            "OutstandingCount" = EXCLUDED."OutstandingCount",
            "VoidedAfterSaleCount" = EXCLUDED."VoidedAfterSaleCount",
            "TotalRevenue" = EXCLUDED."TotalRevenue",
            "RevenueByPeriod" = EXCLUDED."RevenueByPeriod",
            "SalesByPeriod" = EXCLUDED."SalesByPeriod";
        """;

    public async Task<int> RefreshAnalyticsAsync(CancellationToken cancellationToken = default)
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

            logger.LogInformation(
                "Refreshed Library analytics projection ({AffectedRows} rows across all tenants, from source: AccessCodes + SaleRecords).",
                affected);

            return affected;
        });
    }
}
