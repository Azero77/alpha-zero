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
        var currentPeriod = DateTime.UtcNow.ToString("yyyy-MM");
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
        // Arrange: full library flow — sell + redeem a code (activation), then park a
        // FailedProvisioningRecord for THAT code in Courses (as the #58 consumer does when
        // provisioning fails). The sibling code is never activated and must not surface.
        var (tenantId, libraryId, batches) = await SeedLibraryWithBatchesAsync(quantity: 2, faceValue: 50.00m);

        await Client.PostAsJsonAsync($"/library/access-codes/batches/{batches[0].BatchId}/distribute", new { });

        var redeemingUser = Guid.NewGuid();
        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", redeemingUser.ToString());

        var activatedRawCode = batches[0].RawCodes[0];
        var notActivatedRawCode = batches[0].RawCodes[1];

        (await Client.PostAsJsonAsync("/library/access-codes/sell",
            new SellCodeRequest { RawCode = activatedRawCode, LibraryId = libraryId })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = activatedRawCode })).EnsureSuccessStatusCode();

        var activatedAccessCodeId = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .Where(x => x.RedeemedByUserId == redeemingUser)
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

            // Noise: a Provisioned record for the unactivated code — excluded by the consumer.
            coursesDb.FailedProvisioningRecords.Add(new FailedProvisioningRecord(
                Guid.NewGuid(),
                tenantId,
                unactivatedAccessCodeId,
                redeemingUser,
                $"az:course:{tenantId}:course/p4-analytics/plan/standard",
                "resolved meanwhile",
                ProvisioningStatus.Provisioned,
                1,
                DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5), DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)));

            await coursesDb.SaveChangesAsync();
        }

        // Act
        var response = await Client.GetAsync("/library/analytics/provisioning-queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<List<FailedProvisioningDto>>();

        // Assert: the failed activation surfaces with Library-side enrichment (owning library,
        // redeem time); the provisioned/never-activated code does not.
        var item = queue.Should().ContainSingle(q => q.AccessCodeId == activatedAccessCodeId).Subject;
        item.Status.Should().Be("Failed");
        item.UserId.Should().Be(redeemingUser);
        item.LibraryId.Should().Be(libraryId);
        item.RedeemedAt.Should().NotBeNull();
        item.FailureReason.Should().NotBeNullOrEmpty();
        queue.Should().NotContain(q => q.AccessCodeId == unactivatedAccessCodeId);
    }
}
