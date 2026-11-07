using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Analytics;
using AlphaZero.Modules.Library.Infrastructure.Models;
using AlphaZero.Modules.Library.Presentation;
using AlphaZero.Shared.Domain;
using Autofac;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Library.Tests.Integration;

/// <summary>
/// Integration seam for the P4 analytics projection (issue #59): seed a known mix of code states
/// + sale records across tenants, run one refresh, and assert the projection's funnel/revenue
/// numbers exactly; re-run to verify upsert idempotency. Also covers the guarded expiry sweep and
/// the query-service contract (tenant scoping + admin-code sentinel handling).
/// </summary>
public class LibraryAnalyticsSeamTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    private (ILifetimeScope Scope, T Service) ResolveFromModule<T>() where T : notnull
    {
        var module = Resolve<LibraryModule>();
        var scope = module.CreateScope();
        return (scope, scope.Resolve<T>());
    }

    private static AccessCodeDataModel MakeCode(
        Guid tenantId, Guid? libraryId, Guid? batchId, AccessCodeStatus status, DateTime? expiresAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            CodeHash = $"hash-{Guid.NewGuid():N}",
            TenantId = tenantId,
            LibraryId = libraryId,
            StrategyId = "pos-sale",
            TargetResourceArn = ResourceArn.Create($"az:course:{tenantId}:course/analytics-seam").Value,
            Metadata = JsonDocument.Parse("{}"),
            Status = status,
            BatchId = batchId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-30), DateTimeKind.Utc),
            ExpiresAt = expiresAt is null ? null : DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)
        };

    private static readonly DateTime NowUtc = DateTime.UtcNow;
    // Deterministic period anchors: the first day of the current month (UTC) and the last day of
    // the previous month — both always bucket cleanly and are independent of the wall clock.
    private static readonly DateTime CurrentMonthStart = new(NowUtc.Year, NowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PrevMonthDay = CurrentMonthStart.AddDays(-1);
    private static readonly string PrevMonthKey = PrevMonthDay.ToString("yyyy-MM");

    [Fact]
    public async Task RefreshAnalyticsAsync_Should_ProjectFunnelAndRevenue_Exactly_Then_BeIdempotent()
    {
        // Arrange: tenant A has a full lifecycle in Library L1 / Batch B1 plus an admin code
        // (no library, no batch) whose sale is attributed to the sentinel-library row; tenant B
        // has one isolation-proof code.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var libraryA1 = Guid.NewGuid();
        var batchA1 = Guid.NewGuid();
        var libraryB1 = Guid.NewGuid();
        var batchB1 = Guid.NewGuid();

        var mintedCode = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Minted);
        var distributedCode = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Distributed);
        var redeemedA = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Redeemed);
        var redeemedB = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Redeemed);
        var soldCode = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Sold);
        var voidedAfterSaleCode = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Voided);
        var expiredCode = MakeCode(tenantA, libraryA1, batchA1, AccessCodeStatus.Expired);
        var adminCode = MakeCode(tenantA, null, null, AccessCodeStatus.Redeemed);
        var tenantBCode = MakeCode(tenantB, libraryB1, batchB1, AccessCodeStatus.Minted);

        var soldByA = Guid.NewGuid();
        var saleRecords = new[]
        {
            // I-MONEY: all money derives from these five SaleRecord rows, attributed to the
            // selling library of the code whose ID they reference (admin code → sentinel row).
            SaleRecord.Create(tenantA, libraryA1, redeemedA.Id, soldByA, 10.00m, "USD", PrevMonthDay),
            SaleRecord.Create(tenantA, libraryA1, redeemedB.Id, soldByA, 20.50m, "USD", PrevMonthDay),
            SaleRecord.Create(tenantA, libraryA1, soldCode.Id, soldByA, 30.00m, "USD", PrevMonthDay),
            SaleRecord.Create(tenantA, libraryA1, voidedAfterSaleCode.Id, soldByA, 15.25m, "USD", PrevMonthDay),
            SaleRecord.Create(tenantA, libraryA1, adminCode.Id, soldByA, 5.00m, "EUR", PrevMonthDay)
        };

        DbContext.AccessCodes.AddRange(
            mintedCode, distributedCode, redeemedA, redeemedB, soldCode, voidedAfterSaleCode, expiredCode,
            adminCode, tenantBCode);
        DbContext.SaleRecords.AddRange(saleRecords);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear(); // detach seeded rows so later reads aren't served stale tracked copies

        var (scope, refreshService) = ResolveFromModule<ILibraryAnalyticsRefreshService>();
        using (scope)
        {
            // Act: run ONE refresh pass
            await refreshService.RefreshAnalyticsAsync();

            // Assert: exact rows (no-tracking: raw-SQL upserts bypass the tracker, and stale
            // tracked copies would silently satisfy assertions via identity resolution)
            var rowsA = await DbContext.LibraryAnalytics
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantA)
                .ToListAsync();

            rowsA.Should().HaveCount(2); // (L1, B1) and the admin-code sentinel row

            var libBatchRow = rowsA.Single(x => x.LibraryId == libraryA1 && x.BatchId == batchA1);
            libBatchRow.MintedCount.Should().Be(7);
            libBatchRow.DistributedCount.Should().Be(1);
            libBatchRow.SoldCount.Should().Be(4);                              // I-MONEY: from SaleRecords, not 'Sold' status
            libBatchRow.RedeemedCount.Should().Be(2);
            libBatchRow.VoidedCount.Should().Be(1);
            libBatchRow.ExpiredCount.Should().Be(1);
            libBatchRow.OutstandingCount.Should().Be(2);                       // 1 Distributed + 1 Sold
            libBatchRow.VoidedAfterSaleCount.Should().Be(1);                   // ratified voided-after-sale column
            libBatchRow.TotalRevenue.Should().Be(75.75m);                      // 10 + 20.50 + 30 + 15.25 (admin EUR money excluded)
            libBatchRow.SalesByPeriod.Keys.Should().Contain(PrevMonthKey);
            libBatchRow.SalesByPeriod[PrevMonthKey].Should().Be(4);
            libBatchRow.RevenueByPeriod.Should().ContainKey(PrevMonthKey);
            libBatchRow.RevenueByPeriod[PrevMonthKey]["USD"].Should().Be(75.75m);

            var adminRow = rowsA.Single(x => x.LibraryId == Guid.Empty && x.BatchId == Guid.Empty);
            adminRow.MintedCount.Should().Be(1);
            adminRow.SoldCount.Should().Be(1);
            adminRow.TotalRevenue.Should().Be(5.00m);
            adminRow.RevenueByPeriod[PrevMonthKey]["EUR"].Should().Be(5.00m);

            // Tenant isolation within the projection: tenant B's row is separate.
            var rowsB = await DbContext.LibraryAnalytics
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantB)
                .ToListAsync();
            rowsB.Should().ContainSingle(x => x.MintedCount == 1 && x.LibraryId == libraryB1);

            // Act + Assert idempotency: a second pass is an upsert — identical numbers, no duplicate rows.
            await refreshService.RefreshAnalyticsAsync();

            var rowsAfterSecondPass = await DbContext.LibraryAnalytics
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantA)
                .ToListAsync();

            rowsAfterSecondPass.Should().HaveCount(2);
            var libBatchRowAgain = rowsAfterSecondPass.Single(x => x.LibraryId == libraryA1 && x.BatchId == batchA1);
            libBatchRowAgain.MintedCount.Should().Be(7);
            libBatchRowAgain.SoldCount.Should().Be(4);
            libBatchRowAgain.TotalRevenue.Should().Be(75.75m);
            libBatchRowAgain.VoidedAfterSaleCount.Should().Be(1);
            libBatchRowAgain.SalesByPeriod.Should().BeEquivalentTo(libBatchRow.SalesByPeriod);
            libBatchRowAgain.RevenueByPeriod.Should().BeEquivalentTo(libBatchRow.RevenueByPeriod);
        }
    }

    [Fact]
    public async Task SweepExpiredCodesAsync_Should_TransitionThroughStateMachine_And_ProjectAuditRows()
    {
        // Arrange: 3 codes due for expiry (all sweepable states), 1 redeemed code also past expiry
        // (terminal — the state machine forbids Redeemed → Expired and it must remain untouched),
        // plus 1 active code with no expiry at all.
        var tenantId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var past = CurrentMonthStart.AddDays(-1);

        var dueMinted = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Minted, past);
        var dueDistributed = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Distributed, past);
        var dueSold = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Sold, past);
        var terminalRedeemed = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Redeemed, past);
        var activeNoExpiry = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Distributed, null);

        DbContext.AccessCodes.AddRange(dueMinted, dueDistributed, dueSold, terminalRedeemed, activeNoExpiry);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear(); // detach seeded rows so later reads aren't served stale tracked copies

        var (scope, sweepService) = ResolveFromModule<IAccessCodeExpirySweepService>();
        using (scope)
        {
            var swept = await sweepService.SweepExpiredCodesAsync();

            swept.Should().Be(3);

            var statuses = await DbContext.AccessCodes
                .IgnoreQueryFilters()
                .Where(x => new[] { dueMinted.Id, dueDistributed.Id, dueSold.Id, terminalRedeemed.Id, activeNoExpiry.Id }
                    .Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Status);

            statuses[dueMinted.Id].Should().Be(AccessCodeStatus.Expired);
            statuses[dueDistributed.Id].Should().Be(AccessCodeStatus.Expired);
            statuses[dueSold.Id].Should().Be(AccessCodeStatus.Expired);
            statuses[terminalRedeemed.Id].Should().Be(AccessCodeStatus.Redeemed); // untouched (state machine)
            statuses[activeNoExpiry.Id].Should().Be(AccessCodeStatus.Distributed);

            // I-AUDIT: each sweep transition projected exactly one Expired stream row (I-STATE:
            // state mutated only through the state machine, never by direct SQL UPDATE).
            var expiredLogs = await DbContext.LibraryAuditLogs
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.Action == "Expired")
                .ToListAsync();

            expiredLogs.Should().HaveCount(3);
            expiredLogs.Select(x => x.AggregateId).Should().BeEquivalentTo(
                new[] { dueMinted.Id, dueDistributed.Id, dueSold.Id });
        }

        // The refresh reflects the sweep (worker sweeps first, then refreshes, in the same pass).
        var (refreshScope, refreshService) = ResolveFromModule<ILibraryAnalyticsRefreshService>();
        using (refreshScope)
        {
            await refreshService.RefreshAnalyticsAsync();

            var row = await DbContext.LibraryAnalytics
                .IgnoreQueryFilters()
                .SingleAsync(x => x.TenantId == tenantId && x.LibraryId == libraryId && x.BatchId == batchId);

            row.MintedCount.Should().Be(5);
            row.RedeemedCount.Should().Be(1);
            row.ExpiredCount.Should().Be(3);
            row.OutstandingCount.Should().Be(1); // only the activeNoExpiry Distributed code remains
        }
    }

    [Fact]
    public async Task SweepExpiredCodesAsync_Should_ReturnZero_AndCommit_WhenNothingIsDue()
    {
        // Only a NOT-yet-due code exists: the empty branch must commit cleanly and write nothing.
        var tenantId = Guid.NewGuid();
        DbContext.AccessCodes.Add(MakeCode(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), AccessCodeStatus.Distributed,
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc)));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var (scope, sweepService) = ResolveFromModule<IAccessCodeExpirySweepService>();
        using (scope)
        {
            (await sweepService.SweepExpiredCodesAsync()).Should().Be(0);

            (await DbContext.AccessCodes.IgnoreQueryFilters()
                .CountAsync(x => x.TenantId == tenantId && x.Status == AccessCodeStatus.Distributed))
                .Should().Be(1);

            (await DbContext.LibraryAuditLogs.IgnoreQueryFilters()
                .CountAsync(x => x.TenantId == tenantId))
                .Should().Be(0);
        }
    }

    [Fact]
    public async Task RefreshAnalyticsAsync_Should_KeepMultiCurrencyBuckets_WithinOneLibraryRow()
    {
        // Pins the documented money semantics: RevenueByPeriod keeps per-currency buckets inside
        // one (tenant, library, batch) row, while TotalRevenue sums numerically across currencies
        // without conversion (documented simplification — no FX in P4).
        var tenantId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var codeUsd = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Sold);
        var codeEur = MakeCode(tenantId, libraryId, batchId, AccessCodeStatus.Sold);

        DbContext.AccessCodes.AddRange(codeUsd, codeEur);
        DbContext.SaleRecords.AddRange(
            SaleRecord.Create(tenantId, libraryId, codeUsd.Id, Guid.NewGuid(), 50.00m, "USD", PrevMonthDay),
            SaleRecord.Create(tenantId, libraryId, codeEur.Id, Guid.NewGuid(), 45.00m, "EUR", PrevMonthDay));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var (scope, refreshService) = ResolveFromModule<ILibraryAnalyticsRefreshService>();
        using (scope)
        {
            await refreshService.RefreshAnalyticsAsync();

            var row = await DbContext.LibraryAnalytics
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(x => x.TenantId == tenantId);

            row.SoldCount.Should().Be(2);
            row.TotalRevenue.Should().Be(95.00m); // plain decimal sum across currencies (no FX)
            row.RevenueByPeriod[PrevMonthKey].Should().HaveCount(2);
            row.RevenueByPeriod[PrevMonthKey]["USD"].Should().Be(50.00m);
            row.RevenueByPeriod[PrevMonthKey]["EUR"].Should().Be(45.00m);
            row.SalesByPeriod[PrevMonthKey].Should().Be(2);
        }
    }

    [Fact]
    public async Task AnalyticsQueryService_Should_EnforceTenantScoping_And_SentinelHandling()
    {
        // Arrange: projection rows for tenant A (partner library + admin sentinel row + a second
        // library) and tenant B. Reads must scope by tenant: sentinel-library rows roll into
        // totals but are excluded from per-library funnel lines.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var libraryA1 = Guid.NewGuid();
        var libraryA2 = Guid.NewGuid();
        var batchA1 = Guid.NewGuid();
        var libraryB1 = Guid.NewGuid();

        DbContext.LibraryAnalytics.AddRange(
            LibraryAnalytics.Create(tenantA, libraryA1, batchA1,
                10, 4, 3, 2, 1, 0, 5, 1, 100.50m,
                new Dictionary<string, Dictionary<string, decimal>>
                {
                    ["2026-10"] = new() { ["USD"] = 40.00m },
                    ["2026-11"] = new() { ["USD"] = 60.50m }
                },
                new Dictionary<string, int> { ["2026-10"] = 1, ["2026-11"] = 2 }),
            LibraryAnalytics.Create(tenantA, Guid.Empty, Guid.Empty,
                5, 0, 1, 1, 0, 0, 0, 0, 99.00m,
                new Dictionary<string, Dictionary<string, decimal>>
                {
                    ["2026-11"] = new() { ["USD"] = 99.00m }
                },
                new Dictionary<string, int> { ["2026-11"] = 1 }),
            LibraryAnalytics.Create(tenantA, libraryA2, Guid.Empty,
                2, 2, 0, 0, 0, 0, 2, 0, 0m,
                new Dictionary<string, Dictionary<string, decimal>>(),
                new Dictionary<string, int>()),
            LibraryAnalytics.Create(tenantB, libraryB1, Guid.Empty,
                7, 7, 7, 7, 7, 7, 7, 7, 777.77m,
                new Dictionary<string, Dictionary<string, decimal>>
                {
                    ["2026-11"] = new() { ["USD"] = 777.77m }
                },
                new Dictionary<string, int> { ["2026-11"] = 1 }));

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear(); // detach seeded rows so later reads aren't served stale tracked copies

        var module = Resolve<LibraryModule>();
        using var moduleScope = module.CreateScope();
        var queryService = moduleScope.Resolve<AlphaZero.Modules.Library.Application.Analytics.ILibraryAnalyticsQueryService>();
        var provider = moduleScope.Resolve<AlphaZero.Shared.Infrastructure.Tenats.ITenantProvider>() as TestTenantProvider
            ?? throw new InvalidOperationException("TestTenantProvider not resolved from module scope.");

        // Act 1: tenant-wide funnel — totals include the admin sentinel row; batch lines exclude it.
        provider.CurrentTenantId = tenantA;
        var funnel = await queryService.GetLibraryFunnelAsync(null);

        funnel.LibraryId.Should().BeNull();
        funnel.TotalMinted.Should().Be(17);   // 10 + 5 + 2
        funnel.Distributed.Should().Be(6);    // 4 + 2
        funnel.Sold.Should().Be(4);           // 3 + 1 (sale-record sourced)
        funnel.Redeemed.Should().Be(3);       // 2 + 1
        funnel.Voided.Should().Be(1);
        funnel.Expired.Should().Be(0);
        funnel.Outstanding.Should().Be(7);    // 5 + 2
        funnel.VoidedAfterSale.Should().Be(1);
        funnel.Batches.Should().NotContain(b => b.LibraryId == Guid.Empty);
        funnel.Batches.Should().ContainSingle(b => b.LibraryId == libraryA1 && b.BatchId == batchA1);
        funnel.Batches.Should().ContainSingle(b => b.LibraryId == libraryA2);

        // Act 2: library-scoped funnel (the P2 mechanism Managers ride for their own library)
        var scopedFunnel = await queryService.GetLibraryFunnelAsync(libraryA1);

        scopedFunnel.LibraryId.Should().Be(libraryA1);
        scopedFunnel.TotalMinted.Should().Be(10);
        scopedFunnel.Sold.Should().Be(3);
        scopedFunnel.VoidedAfterSale.Should().Be(1);
        scopedFunnel.Batches.Should().ContainSingle();

        // Act 3: tenant-wide revenue — money includes admin codes; the Libraries rollup carries a
        // null-LibraryId admin entry; tenant B's rows are invisible.
        var revenue = await queryService.GetLibraryRevenueAsync(null);

        revenue.TotalRevenue.Should().Be(199.50m);  // 100.50 + 99.00
        revenue.TotalSales.Should().Be(4);          // 3 + 1
        revenue.Libraries.Should().Contain(l => l.LibraryId == libraryA1 && l.TotalRevenue == 100.50m);
        revenue.Libraries.Should().Contain(l => l.LibraryId == null && l.TotalRevenue == 99.00m);
        revenue.Libraries.Should().NotContain(l => l.LibraryId == libraryB1);
        var period10 = revenue.Periods.Should().ContainSingle(p => p.Period == "2026-10").Subject;
        period10.SalesCount.Should().Be(1);
        period10.ByCurrency.Should().Contain(c => c.Currency == "USD" && c.Revenue == 40.00m);
        var period11 = revenue.Periods.Should().ContainSingle(p => p.Period == "2026-11").Subject;
        period11.SalesCount.Should().Be(3);         // 2 (L1) + 1 (admin)
        period11.ByCurrency.Should().Contain(c => c.Currency == "USD" && c.Revenue == 159.50m); // 60.50 + 99.00

        // Act 4: cross-tenant leak check with tenant B context
        provider.CurrentTenantId = tenantB;
        var funnelB = await queryService.GetLibraryFunnelAsync(null);
        funnelB.TotalMinted.Should().Be(7);
        funnelB.Sold.Should().Be(7);
        funnelB.Batches.Should().ContainSingle(b => b.LibraryId == libraryB1);

        var revenueB = await queryService.GetLibraryRevenueAsync(null);
        revenueB.TotalRevenue.Should().Be(777.77m);
        revenueB.Libraries.Should().ContainSingle(l => l.LibraryId == libraryB1);
    }
}
