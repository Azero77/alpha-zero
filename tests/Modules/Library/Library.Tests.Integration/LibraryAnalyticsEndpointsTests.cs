using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using CoursesAppDbContext = AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetFraudFlags;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryFunnel;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryRevenue;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;
using Autofac;
using AlphaZero.Modules.Library.Infrastructure.Analytics;
using AlphaZero.Modules.Library.Presentation;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.SellCode;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Library.Tests.Integration;

/// <summary>
/// HTTP seam for the P4 analytics endpoints (issue #59): drives full Library flows (mint →
/// distribute → sell → redeem → void), runs one analytics refresh, verifies funnel/revenue
/// numbers over HTTP, and checks the fraud/provisioning queues with linked audit entries.
/// </summary>
public class LibraryAnalyticsEndpointsTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<(Guid TenantId, Guid LibraryId, List<(Guid BatchId, List<string> RawCodes)> Batches)>
        SeedLibraryWithBatchesAsync(int quantity, decimal faceValue, int batchCount = 1)
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/p4-analytics";

        var libRes = await Client.PostAsJsonAsync("/library/libraries", new CreateLibraryRequest
        {
            Name = "P4 Analytics Lib",
            Address = "Address 1",
            ContactNumber = "123456"
        });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", new AuthorizeResourceRequest
        {
            Id = libraryId,
            ResourceArn = courseArn
        });

        var batches = new List<(Guid BatchId, List<string> RawCodes)>();
        for (var i = 0; i < batchCount; i++)
        {
            var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
                new GenerateBatchRequest
                {
                    LibraryId = libraryId,
                    Quantity = quantity,
                    TargetResourceArn = courseArn,
                    FaceValue = faceValue
                });
            genRes.EnsureSuccessStatusCode();
            var genData = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();
            batches.Add((genData!.BatchId, genData.Codes));
        }

        return (tenantId, libraryId, batches);
    }

    [Fact]
    public async Task Funnel_And_Revenue_Endpoints_Should_ReturnNumbers_AfterLifecycleFlow()
    {
        // Flow clock captured BEFORE any sale (same UTC month as the eventual bucket: sales
        // happen seconds later, so a just-crossed month boundary is excluded by construction).
        var flowStartedUtc = DateTime.UtcNow;
        var (tenantId, libraryId, batches) = await SeedLibraryWithBatchesAsync(quantity: 4, faceValue: 50.00m);
        var batchId = batches[0].BatchId;

        // Distribute the whole batch
        var distRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/distribute", new { });
        distRes.EnsureSuccessStatusCode();

        var rawCodes = batches[0].RawCodes;
        var soldCodeA = rawCodes[0];
        var soldCodeB = rawCodes[1];

        // Sell two codes (POST /library/access-codes/sell), one per user
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        (await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest { RawCode = soldCodeA, LibraryId = libraryId }))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest { RawCode = soldCodeB, LibraryId = libraryId }))
            .EnsureSuccessStatusCode();

        // Redeem the first sold code; redeem then void the second one (void-after-redeem fraud signal)
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        (await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = soldCodeA })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = soldCodeB })).EnsureSuccessStatusCode();

        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        (await Client.PostAsJsonAsync("/library/access-codes/void",
            new AlphaZero.Modules.Library.Application.AccessCodes.VoidCode.VoidCodeCommand(soldCodeB, "Recovered stolen card")))
            .EnsureSuccessStatusCode();

        // Run one analytics pass
        var module = Resolve<LibraryModule>();
        using var moduleScope = module.CreateScope();
        await moduleScope.Resolve<ILibraryAnalyticsRefreshService>().RefreshAnalyticsAsync();

        // Funnel: minted 4, distributed 2 (the two still-Distributed codes — current-status
        // semantics), sold 2 (sale records!), redeemed 1, voided 1, outstanding 2 (Distributed+Sold
        // statuses), voided-after-sale 1.
        var funnelRes = await Client.GetAsync("/library/analytics/funnel");
        funnelRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var funnel = await funnelRes.Content.ReadFromJsonAsync<LibraryFunnelDto>();
        funnel!.LibraryId.Should().BeNull();                      // tenant-wide
        funnel.TotalMinted.Should().Be(4);
        funnel.Distributed.Should().Be(2);
        funnel.Sold.Should().Be(2);
        funnel.Redeemed.Should().Be(1);
        funnel.Voided.Should().Be(1);
        funnel.Expired.Should().Be(0);
        funnel.Outstanding.Should().Be(2);
        funnel.VoidedAfterSale.Should().Be(1);
        funnel.Batches.Should().ContainSingle(b => b.BatchId == batchId && b.LibraryId == libraryId)
            .Which.VoidedAfterSale.Should().Be(1);

        // Revenue: 2 face-value sales of 50 = 100, current (UTC) period
        var revenueRes = await Client.GetAsync("/library/analytics/revenue");
        revenueRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var revenue = await revenueRes.Content.ReadFromJsonAsync<LibraryRevenueDto>();
        revenue!.TotalRevenue.Should().Be(100.00m);
        revenue.TotalSales.Should().Be(2);
        revenue.Libraries.Should().ContainSingle(l => l.LibraryId == libraryId && l.TotalRevenue == 100.00m);
        var currentPeriod = flowStartedUtc.ToString("yyyy-MM");
        var period = revenue.Periods.Should().ContainSingle(p => p.Period == currentPeriod).Subject;
        period.SalesCount.Should().Be(2);
        period.ByCurrency.Should().Contain(c => c.Currency == "USD" && c.Revenue == 100.00m);

        // Fraud queue: the void-after-redeem flow must surface with its linked audit entries
        var fraudRes = await Client.GetAsync("/library/analytics/fraud-queue");
        fraudRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var flags = await fraudRes.Content.ReadFromJsonAsync<List<FraudFlagDto>>();
        var voidAfterRedeem = flags.Should().ContainSingle(f => f.Type == "VoidAfterRedeem").Subject;
        voidAfterRedeem.LibraryId.Should().Be(libraryId);
        voidAfterRedeem.RelatedAuditEntries.Should().HaveCount(2); // Redeemed + Voided
        voidAfterRedeem.RelatedAuditEntries.Select(e => e.Action).Should().BeEquivalentTo(new[] { "Redeemed", "Voided" });
        voidAfterRedeem.RelatedAuditEntries.Should().OnlyContain(e => e.EventId != Guid.Empty);
    }

    [Fact]
    public async Task FraudQueue_Should_IncludeUnsoldRedemptions_And_VelocityAnomalies()
    {
        var (tenantId, libraryId, batches) = await SeedLibraryWithBatchesAsync(quantity: 1, faceValue: 25.00m);

        // redeem WITHOUT a prior sale → UnsoldRedemption flag
        var rawCode = batches[0].RawCodes[0];
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        (await Client.PostAsJsonAsync($"/library/access-codes/batches/{batches[0].BatchId}/distribute", new { })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = rawCode })).EnsureSuccessStatusCode();

        // Redemption velocity anomaly: seed 11 Redeemed audit rows for the same library/day
        // (the phantom-code stream only — queue detection reads the audit stream per-request).
        for (var i = 0; i < 11; i++)
        {
            var log = AlphaZero.Modules.Library.Domain.LibraryAuditLog.Create(
                Guid.NewGuid(),
                tenantId,
                libraryId,
                Guid.NewGuid(),
                "Redeemed",
                Guid.NewGuid(),
                System.Text.Json.JsonDocument.Parse("""{"UserId":"seeded","UnsoldRedemption":false}"""),
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));
            DbContext.LibraryAuditLogs.Add(log);
        }
        await DbContext.SaveChangesAsync();

        // Lookback bound (LibraryAnalytics:FraudLookbackDays, default 30): an UnsoldRedemption
        // entry 45 days back is OUTSIDE the request-time read window — the queue read is
        // bounded and must not surface history older than the cutoff.
        DbContext.LibraryAuditLogs.Add(AlphaZero.Modules.Library.Domain.LibraryAuditLog.Create(
            Guid.NewGuid(),
            tenantId,
            libraryId,
            Guid.NewGuid(),
            "Redeemed",
            Guid.NewGuid(),
            System.Text.Json.JsonDocument.Parse("""{"UserId":"seeded","UnsoldRedemption":true}"""),
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-45), DateTimeKind.Utc)));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var fraudRes = await Client.GetAsync("/library/analytics/fraud-queue");
        fraudRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var flags = await fraudRes.Content.ReadFromJsonAsync<List<FraudFlagDto>>();

        flags!.Should().ContainSingle(f => f.Type == "UnsoldRedemption")
            .Which.RelatedAuditEntries.Should().ContainSingle(e => e.Action == "Redeemed");

        var velocity = flags.Should().ContainSingle(f => f.Type == "RedemptionVelocity").Subject;
        velocity.LibraryId.Should().Be(libraryId);
        velocity.Details.Should().Contain("exceed the configured threshold");
    }

    [Fact]
    public async Task ProvisioningQueue_Should_ReconcileLibraryActivation_WithCoursesProvisioningState()
    {
        // Arrange: full library flow — sell + redeem two codes (both activations), then park
        // provisioning records in Courses: a Failed one for the first code (as the #58 consumer
        // does when provisioning fails) and a Provisioned one for the second code. The second
        // code PASSES the activation join but must not surface (status filter) — the two
        // mechanisms are therefore asserted independently:
        //   • activation join excludes the never-activated code,
        //   • status filter excludes the resolved (Provisioned) state.
        var (tenantId, libraryId, batches) = await SeedLibraryWithBatchesAsync(quantity: 2, faceValue: 50.00m, batchCount: 2);

        await Client.PostAsJsonAsync($"/library/access-codes/batches/{batches[0].BatchId}/distribute", new { });
        await Client.PostAsJsonAsync($"/library/access-codes/batches/{batches[1].BatchId}/distribute", new { });

        var redeemingUser = Guid.NewGuid();
        var redeemingUser2 = Guid.NewGuid();
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", redeemingUser.ToString());

        var activatedRawCode = batches[0].RawCodes[0];
        var notActivatedRawCode = batches[0].RawCodes[1];

        (await Client.PostAsJsonAsync("/library/access-codes/sell",
            new SellCodeRequest { RawCode = activatedRawCode, LibraryId = libraryId })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = activatedRawCode })).EnsureSuccessStatusCode();

        // Second activation: a DIFFERENT activated code whose provisioning was resolved.
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", redeemingUser2.ToString());
        var resolvedRawCode = batches[1].RawCodes[0];
        (await Client.PostAsJsonAsync("/library/access-codes/sell",
            new SellCodeRequest { RawCode = resolvedRawCode, LibraryId = libraryId })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = resolvedRawCode })).EnsureSuccessStatusCode();
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", redeemingUser.ToString());

        var activatedAccessCodeId = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .Where(x => x.RedeemedByUserId == redeemingUser)
            .Select(x => x.Id)
            .SingleAsync();

        var resolvedAccessCodeId = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .Where(x => x.RedeemedByUserId == redeemingUser2)
            .Select(x => x.Id)
            .SingleAsync();

        // Not activated: no Redeemed audit entry → not a queue candidate even if Courses has state.
        var unactivatedAccessCodeId = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .Where(x => x.BatchId == batches[0].BatchId && x.Id != activatedAccessCodeId)
            .Select(x => x.Id)
            .SingleAsync();

        using (var coursesScope = Factory.Services.CreateScope())
        {
            var coursesDb = coursesScope.ServiceProvider.GetRequiredService<CoursesAppDbContext>();
            coursesDb.FailedProvisioningRecords.Add(new FailedProvisioningRecord(
                Guid.NewGuid(),
                tenantId,
                activatedAccessCodeId,
                redeemingUser,
                $"az:course:{tenantId}:course/p4-analytics/plan/standard",
                "Course 'plan' could not be resolved.",
                ProvisioningStatus.Failed,
                1,
                DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5), DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)));

            // Noise (discriminating): a Provisioned record for the SECOND activated code.
            // The activation join WOULD match this code's id (it was redeemed), so the only
            // reason it must not surface is the consumer's non-Provisioned status filter.
            // One row per (TenantId, AccessCodeId) — the unique index forbids parking both a
            // Failed and a Provisioned row for the same code, mirroring the upsert semantics.
            coursesDb.FailedProvisioningRecords.Add(new FailedProvisioningRecord(
                Guid.NewGuid(),
                tenantId,
                resolvedAccessCodeId,
                redeemingUser2,
                $"az:course:{tenantId}:course/p4-analytics/plan/standard",
                "resolved meanwhile",
                ProvisioningStatus.Provisioned,
                1,
                DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-4), DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)));

            await coursesDb.SaveChangesAsync();
        }

        // Act
        var response = await Client.GetAsync("/library/analytics/provisioning-queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<FailedProvisioningDto>>();

        // Assert: the failed activation surfaces with Library-side enrichment (owning library,
        // redeem time); the resolved (Provisioned) activation never surfaces (status filter) and
        // the never-activated code does not (activation join).
        var failedItem = queue.Should()
            .ContainSingle(q => q.AccessCodeId == activatedAccessCodeId && q.Status == "Failed").Subject;
        failedItem.UserId.Should().Be(redeemingUser);
        failedItem.LibraryId.Should().Be(libraryId);
        failedItem.RedeemedAt.Should().NotBeNull();
        failedItem.FailureReason.Should().NotBeNullOrEmpty();
        queue.Should().NotContain(q => q.AccessCodeId == resolvedAccessCodeId);
        queue.Should().NotContain(q => q.AccessCodeId == unactivatedAccessCodeId);

        // Library-scoped read: the endpoint ARN already caps authorization; the query must also
        // narrow to the requested library only.
        var scopedResponse = await Client.GetAsync($"/library/analytics/provisioning-queue?libraryId={libraryId}");
        scopedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scopedResponse.Content.ReadFromJsonAsync<List<FailedProvisioningDto>>())
            .Should().ContainSingle(q => q.AccessCodeId == activatedAccessCodeId);

        var otherLibraryResponse = await Client.GetAsync($"/library/analytics/provisioning-queue?libraryId={Guid.NewGuid()}");
        otherLibraryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await otherLibraryResponse.Content.ReadFromJsonAsync<List<FailedProvisioningDto>>()).Should().BeEmpty();
    }
}
