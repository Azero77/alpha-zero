using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Library.Infrastructure.Models;

/// <summary>
/// Projection row for the Library analytics surface (issue #59). Infrastructure-owned read model —
/// NOT a domain aggregate (mirrors CourseAnalytics). Refreshed by a set-based UPSERT in
/// <c>LibraryAnalyticsRefreshService</c>; never written by request paths.
/// Invariants:
/// - I-MONEY: sold/revenue columns derive exclusively from SaleRecord rows.
/// - I-TENANT: rows are tenant-scoped; reads go through tenant-scoped queries.
/// - Sentinel semantics: <c>LibraryId</c> = Guid.Empty denotes codes without an owning library
///   (admin codes); <c>BatchId</c> = Guid.Empty denotes codes minted without a batch. Sentinels keep
///   the composite key non-nullable so ON CONFLICT upserts work.
/// </summary>
public class LibraryAnalytics : IDomainTenantOwned
{
    public static readonly Guid NoLibrarySentinel = Guid.Empty;
    public static readonly Guid NoBatchSentinel = Guid.Empty;

    public Guid TenantId { get; private set; }
    public Guid LibraryId { get; private set; }
    public Guid BatchId { get; private set; }

    public int MintedCount { get; private set; }
    public int DistributedCount { get; private set; }
    public int SoldCount { get; private set; }
    public int RedeemedCount { get; private set; }
    public int VoidedCount { get; private set; }
    public int ExpiredCount { get; private set; }
    public int OutstandingCount { get; private set; }
    public int VoidedAfterSaleCount { get; private set; }

    public decimal TotalRevenue { get; private set; }

    /// <summary>"yyyy-MM" (UTC) → currency → revenue at face value.</summary>
    public Dictionary<string, Dictionary<string, decimal>> RevenueByPeriod { get; private set; }

    /// <summary>"yyyy-MM" (UTC) → number of SaleRecord rows in that period.</summary>
    public Dictionary<string, int> SalesByPeriod { get; private set; }

    private LibraryAnalytics()
    {
        RevenueByPeriod = new Dictionary<string, Dictionary<string, decimal>>();
        SalesByPeriod = new Dictionary<string, int>();
    }

    public static LibraryAnalytics Create(
        Guid tenantId,
        Guid libraryId,
        Guid batchId,
        int mintedCount,
        int distributedCount,
        int soldCount,
        int redeemedCount,
        int voidedCount,
        int expiredCount,
        int outstandingCount,
        int voidedAfterSaleCount,
        decimal totalRevenue,
        Dictionary<string, Dictionary<string, decimal>> revenueByPeriod,
        Dictionary<string, int> salesByPeriod)
    {
        return new LibraryAnalytics
        {
            TenantId = tenantId,
            LibraryId = libraryId,
            BatchId = batchId,
            MintedCount = mintedCount,
            DistributedCount = distributedCount,
            SoldCount = soldCount,
            RedeemedCount = redeemedCount,
            VoidedCount = voidedCount,
            ExpiredCount = expiredCount,
            OutstandingCount = outstandingCount,
            VoidedAfterSaleCount = voidedAfterSaleCount,
            TotalRevenue = totalRevenue,
            RevenueByPeriod = revenueByPeriod,
            SalesByPeriod = salesByPeriod
        };
    }
}
