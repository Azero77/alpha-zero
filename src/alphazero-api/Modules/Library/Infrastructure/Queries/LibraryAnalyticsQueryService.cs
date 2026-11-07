using System.Text.Json;
using AlphaZero.Modules.Library.Application.Analytics;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetFraudFlags;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryFunnel;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryRevenue;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Models;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Infrastructure.Queries;

/// <summary>
/// Analytics read side (issue #59). Reads the <c>LibraryAnalytics</c> projection for funnel and
/// revenue (tenant-scoped by construction: EF global tenant filter + explicit TenantId predicate,
/// mirroring LibraryQueryService). The fraud queue is computed per-request over the append-only
/// audit stream; the provisioning queue joins Library's Redeemed audit entries with Courses'
/// provisioning state via the cross-module bus contract.
/// </summary>
public class LibraryAnalyticsQueryService(
    AppDbContext context,
    ITenantProvider tenantProvider,
    IProvisioningStateReadService provisioningStateReadService,
    IConfiguration configuration,
    ILogger<LibraryAnalyticsQueryService> logger) : ILibraryAnalyticsQueryService
{
    // Fraud velocity threshold: redemptions per library per UTC day above this value raise a
    // flag. Anomalous redemption velocity is a classic resale/staff-skimming signal (issue #59).
    public const string VelocityThresholdConfigKey = "LibraryAnalytics:FraudRedemptionVelocityThreshold";
    public const int DefaultFraudRedemptionVelocityThreshold = 10;

    // Bounded lookback for all request-time audit-stream reads (fraud + provisioning queue).
    // The stream is append-only and grows without limit; without a window every request would
    // scan the tenant's entire history. 30 UTC days covers all realistic fraud/reconciliation
    // hardening checks while keeping the reads bounded.
    public const string FraudLookbackDaysConfigKey = "LibraryAnalytics:FraudLookbackDays";
    public const int DefaultFraudLookbackDays = 30;

    private int ResolveVelocityThreshold()
    {
        var raw = configuration[VelocityThresholdConfigKey];
        if (int.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;
        return DefaultFraudRedemptionVelocityThreshold;
    }

    private int ResolveRepositoryLookbackDays()
    {
        var raw = configuration[FraudLookbackDaysConfigKey];
        if (int.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;
        return DefaultFraudLookbackDays;
    }

    private sealed record ActivatedCodeRef(Guid AccessCodeId, Guid? LibraryId, DateTime OccurredAt);

    private sealed record FunnelTotals(
        int Minted, int Distributed, int Sold, int Redeemed, int Voided, int Expired, int Outstanding, int VoidedAfterSale);

    private async Task<List<LibraryAnalytics>> ScopedRows(
        Guid tenantId, Guid? libraryId, CancellationToken cancellationToken)
    {
        var query = context.LibraryAnalytics
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (libraryId.HasValue)
            query = query.Where(x => x.LibraryId == libraryId.Value);

        return await query
            .OrderBy(x => x.LibraryId)
            .ThenBy(x => x.BatchId)
            .ToListAsync(cancellationToken);
    }

    private static FunnelTotals AggregateFunnel(IEnumerable<LibraryAnalytics> rows)
    {
        var list = rows.ToList();
        return new FunnelTotals(
            Minted: list.Sum(x => x.MintedCount),
            Distributed: list.Sum(x => x.DistributedCount),
            Sold: list.Sum(x => x.SoldCount),
            Redeemed: list.Sum(x => x.RedeemedCount),
            Voided: list.Sum(x => x.VoidedCount),
            Expired: list.Sum(x => x.ExpiredCount),
            Outstanding: list.Sum(x => x.OutstandingCount),
            VoidedAfterSale: list.Sum(x => x.VoidedAfterSaleCount));
    }

    private static LibraryBatchFunnelDto MapBatch(LibraryAnalytics row) => new(
        row.LibraryId,
        row.BatchId,
        row.MintedCount,
        row.DistributedCount,
        row.SoldCount,
        row.RedeemedCount,
        row.VoidedCount,
        row.ExpiredCount,
        row.OutstandingCount,
        row.VoidedAfterSaleCount);

    private static LibraryFunnelDto CreateEmptyFunnel(Guid? libraryId) => new(
        libraryId, 0, 0, 0, 0, 0, 0, 0, 0, new List<LibraryBatchFunnelDto>());

    public async Task<LibraryFunnelDto> GetLibraryFunnelAsync(Guid? libraryId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return CreateEmptyFunnel(libraryId);

        var rows = await ScopedRows(tenantId.Value, libraryId, cancellationToken);
        var totals = AggregateFunnel(rows);

        // Admin codes (sentinel library) roll into totals but not into per-library batch lines.
        var batchLines = rows
            .Where(x => x.LibraryId != LibraryAnalytics.NoLibrarySentinel)
            .Select(MapBatch)
            .ToList();

        return new LibraryFunnelDto(
            libraryId,
            totals.Minted,
            totals.Distributed,
            totals.Sold,
            totals.Redeemed,
            totals.Voided,
            totals.Expired,
            totals.Outstanding,
            totals.VoidedAfterSale,
            batchLines);
    }

    private static LibraryRevenueDto CreateEmptyRevenue(Guid? libraryId) => new(
        libraryId, 0m, 0, new List<LibraryRevenuePeriodDto>(), new List<LibraryRevenueLibraryDto>());

    public async Task<LibraryRevenueDto> GetLibraryRevenueAsync(Guid? libraryId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return CreateEmptyRevenue(libraryId);

        var rows = await ScopedRows(tenantId.Value, libraryId, cancellationToken);

        var revenueByPeriod = new Dictionary<string, (int SalesCount, Dictionary<string, decimal> Currencies)>();
        foreach (var row in rows)
        {
            var salesByPeriod = row.SalesByPeriod ?? new Dictionary<string, int>();
            var rowRevenueByPeriod = row.RevenueByPeriod ?? new Dictionary<string, Dictionary<string, decimal>>();

            foreach (var period in salesByPeriod.Keys.Union(rowRevenueByPeriod.Keys))
            {
                var bucket = revenueByPeriod.TryGetValue(period, out var existing)
                    ? existing
                    : (SalesCount: 0, Currencies: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));

                if (salesByPeriod.TryGetValue(period, out var salesCount))
                {
                    bucket.SalesCount += salesCount;
                }

                if (rowRevenueByPeriod.TryGetValue(period, out var currencies))
                {
                    foreach (var (currency, amount) in currencies)
                    {
                        bucket.Currencies[currency] = bucket.Currencies.TryGetValue(currency, out var cur) ? cur + amount : amount;
                    }
                }

                revenueByPeriod[period] = bucket;
            }
        }

        var periods = revenueByPeriod
            .OrderByDescending(kvp => kvp.Key)
            .Select(kvp => new LibraryRevenuePeriodDto(
                kvp.Key,
                kvp.Value.SalesCount,
                kvp.Value.Currencies
                    .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new LibraryRevenueCurrencyDto(c.Key, c.Value))
                    .ToList()))
            .ToList();

        // Sentinel-library group carries admin-code money; surfaced with LibraryId null so it is
        // visible in tenant-wide rollups (partner-library lines are separate).
        var libraries = rows
            .GroupBy(x => x.LibraryId)
            .Select(g => new LibraryRevenueLibraryDto(
                g.Key == LibraryAnalytics.NoLibrarySentinel ? null : g.Key,
                g.Sum(x => x.TotalRevenue),
                g.Sum(x => x.SoldCount)))
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        return new LibraryRevenueDto(
            libraryId,
            rows.Sum(x => x.TotalRevenue),
            rows.Sum(x => x.SoldCount),
            periods,
            libraries);
    }

    public async Task<List<FraudFlagDto>> GetFraudFlagsAsync(Guid? libraryId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return new List<FraudFlagDto>();

        var query = context.LibraryAuditLogs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value
                        && (x.Action == "Redeemed" || x.Action == "Voided")
                        && x.OccurredAt >= DateTime.UtcNow.AddDays(-ResolveRepositoryLookbackDays()));

        if (libraryId.HasValue)
            query = query.Where(x => x.LibraryId == libraryId.Value);

        var logs = (await query.ToListAsync(cancellationToken))
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.EventId)
            .ToList();

        var flags = new List<FraudFlagDto>();
        var byAggregate = logs.GroupBy(x => x.AggregateId).ToList();

        // 1. Unsold redemptions: Redeemed entries flagged with payload UnsoldRedemption = true
        //    (no SaleRecord existed at redemption time; P3a lax gate raised the flag).
        foreach (var log in logs.Where(IsUnsoldRedemption))
        {
            flags.Add(new FraudFlagDto(
                "UnsoldRedemption",
                log.AggregateId,
                log.LibraryId,
                log.OccurredAt,
                "Code was redeemed without a recorded sale (UnsoldRedemption flag in the redeem audit entry).",
                new List<LibraryAuditEntryRefDto> { Ref(log) }));
        }

        // 2. Void-after-redeem: a code redeemed earlier and voided afterwards (potential refund-as-void abuse).
        foreach (var group in byAggregate)
        {
            var timeline = group.OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId).ToList();
            var firstRedeemIndex = timeline.FindIndex(x => x.Action == "Redeemed");
            var voidEntry = timeline.FindLastIndex(x => x.Action == "Voided");

            if (firstRedeemIndex < 0 || voidEntry < 0 || voidEntry <= firstRedeemIndex)
                continue;

            var redeemed = timeline[firstRedeemIndex];
            var voided = timeline[voidEntry];
            var related = new[] { Ref(redeemed), Ref(voided) };

            flags.Add(new FraudFlagDto(
                "VoidAfterRedeem",
                group.Key,
                voided.LibraryId ?? redeemed.LibraryId,
                voided.OccurredAt,
                $"Code was redeemed at {redeemed.OccurredAt:u} and voided afterwards (reason: {ReadVoidReason(voided)}).",
                related.ToList()));
        }

        // 3. Redemption velocity: redemptions per library per UTC day above the configured threshold.
        var threshold = ResolveVelocityThreshold();
        var velocityGroups = logs.Where(x => x.Action == "Redeemed")
            .GroupBy(x => (LibraryId: x.LibraryId, Day: x.OccurredAt.Date));

        foreach (var group in velocityGroups.Where(g => g.Count() > threshold))
        {
            var entries = group.OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId).ToList();
            var last = entries[^1];

            flags.Add(new FraudFlagDto(
                "RedemptionVelocity",
                Guid.Empty,
                group.Key.LibraryId,
                last.OccurredAt,
                $"{group.Count()} redemptions on {group.Key.Day:yyyy-MM-dd} for library {group.Key.LibraryId?.ToString() ?? "(unassigned)"} exceed the configured threshold of {threshold}.",
                entries.Select(Ref).ToList()));
        }

        return flags
            .OrderByDescending(x => x.OccurredAt)
            .ThenBy(x => x.Type)
            .ToList();
    }

    private static bool IsUnsoldRedemption(LibraryAuditLog log)
    {
        if (log.Action != "Redeemed")
            return false;

        try
        {
            if (log.Payload?.RootElement.TryGetProperty("UnsoldRedemption", out var flag) != true)
                return false;

            return flag.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadVoidReason(LibraryAuditLog voidedLog)
    {
        try
        {
            return voidedLog.Payload?.RootElement.TryGetProperty("Reason", out var reason) == true
                ? reason.GetString() ?? "(none)"
                : "(none)";
        }
        catch (JsonException)
        {
            return "(none)";
        }
    }

    private static LibraryAuditEntryRefDto Ref(LibraryAuditLog log) =>
        new(log.EventId, log.AggregateId, log.Action, log.ActorUserId, log.OccurredAt);

    private static List<FailedProvisioningDto> CreateEmptyProvisioningQueue() => new();

    public async Task<List<FailedProvisioningDto>> GetProvisioningQueueAsync(Guid? libraryId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null || tenantId == Guid.Empty)
            return CreateEmptyProvisioningQueue();

        // Activated = Redeemed entries in the audit stream (latest occurrence per code), bounded
        // to the same lookback window as the fraud queue so the read cannot grow unbounded.
        var activated = await context.LibraryAuditLogs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value
                        && x.Action == "Redeemed"
                        && x.OccurredAt >= DateTime.UtcNow.AddDays(-ResolveRepositoryLookbackDays()))
            .Select(x => new ActivatedCodeRef(x.AggregateId, x.LibraryId, x.OccurredAt))
            .ToListAsync(cancellationToken);

        var latestPerCode = activated
            .GroupBy(x => x.AccessCodeId)
            .Select(g => g.OrderByDescending(x => x.OccurredAt).First())
            .ToList();

        // Data minimization: a library-scoped request (Manager ARN) must not drive a tenant-wide
        // cross-module exchange — narrow the activation set BEFORE the bus round-trip.
        if (libraryId.HasValue)
            latestPerCode = latestPerCode.Where(x => x.LibraryId == libraryId.Value).ToList();

        if (latestPerCode.Count == 0)
            return CreateEmptyProvisioningQueue();

        var provisioningState = await provisioningStateReadService.GetProvisioningStateAsync(
            tenantId.Value,
            latestPerCode.Select(x => x.AccessCodeId).ToList(),
            cancellationToken);

        var activationByCode = latestPerCode.ToDictionary(x => x.AccessCodeId, x => x);

        var queue = provisioningState
            .Select(item =>
            {
                var activation = activationByCode.TryGetValue(item.AccessCodeId, out var found) ? found : null;
                return new FailedProvisioningDto(
                    item.AccessCodeId,
                    item.UserId,
                    item.TargetResourceArn,
                    item.Status,
                    item.FailureReason,
                    item.RetryCount,
                    item.CreatedAt,
                    item.LastAttemptedAt,
                    LibraryId: activation?.LibraryId,
                    RedeemedAt: activation?.OccurredAt);
            })
            .Where(item => !libraryId.HasValue || item.LibraryId == libraryId.Value)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();

        if (logger.IsEnabled(LogLevel.Debug) && queue.Count > 0)
        {
            logger.LogDebug(
                "Provisioning queue: {Count} activated codes have non-provisioned state in Courses.",
                queue.Count);
        }

        return queue;
    }
}
