using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.SellCode;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Library.Tests.Integration;

public class SellCodeTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<(Guid LibraryId, Guid BatchId, List<string> RawCodes)> SetupDistributedBatchAsync(Guid tenantId, decimal faceValue = 50m)
    {
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/sell-test-{Guid.NewGuid():N}";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "POS Library", Address = "Main St", ContactNumber = "123" });
        libRes.EnsureSuccessStatusCode();
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        var authRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });
        authRes.EnsureSuccessStatusCode();

        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 3,
                TargetResourceArn = courseArn,
                FaceValue = faceValue
            });
        genRes.EnsureSuccessStatusCode();
        var batchResult = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();

        var batchId = batchResult!.BatchId;
        var rawCodes = batchResult.Codes;

        var distRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/distribute", new { });
        distRes.EnsureSuccessStatusCode();

        return (libraryId, batchId, rawCodes);
    }

    [Fact]
    public async Task SellCode_Should_Return201_And_PersistSaleRecordAndSoldStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (libraryId, _, rawCodes) = await SetupDistributedBatchAsync(tenantId, faceValue: 85.50m);
        var rawCodeToSell = rawCodes[0];

        // Act: POS records the sale of code
        var sellRequest = new SellCodeRequest
        {
            RawCode = rawCodeToSell,
            LibraryId = libraryId
        };
        var response = await Client.PostAsJsonAsync("/library/access-codes/sell", sellRequest);

        // Assert: 201 Created with sale details
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sellResponse = await response.Content.ReadFromJsonAsync<SellCodeResponse>();
        sellResponse.Should().NotBeNull();
        sellResponse!.Id.Should().NotBeEmpty();
        sellResponse.LibraryId.Should().Be(libraryId);
        sellResponse.Price.Should().Be(85.50m);
        sellResponse.Currency.Should().Be("USD");
        sellResponse.SoldAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // Invariant I-STATE: AccessCode status is moved to Sold
        var codeInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.Id == sellResponse.AccessCodeId);
        codeInDb.Status.Should().Be(AccessCodeStatus.Sold);

        // Invariant I-MONEY: SaleRecord exists with snapshotted price
        var saleRecordInDb = await DbContext.SaleRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(s => s.Id == sellResponse.Id);
        saleRecordInDb.AccessCodeId.Should().Be(codeInDb.Id);
        saleRecordInDb.LibraryId.Should().Be(libraryId);
        saleRecordInDb.TenantId.Should().Be(tenantId);
        saleRecordInDb.Price.Should().Be(85.50m);
        saleRecordInDb.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task SellCode_Twice_Should_Return409Conflict()
    {
        // Arrange: Sell code once
        var tenantId = Guid.NewGuid();
        var (libraryId, _, rawCodes) = await SetupDistributedBatchAsync(tenantId, faceValue: 50m);
        var rawCode = rawCodes[0];

        var firstRes = await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest
        {
            RawCode = rawCode,
            LibraryId = libraryId
        });
        firstRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Attempt to sell same code a second time
        var secondRes = await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest
        {
            RawCode = rawCode,
            LibraryId = libraryId
        });

        // Assert: 409 Conflict (AccessCode.AlreadySold)
        secondRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await secondRes.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("title").GetString().Should().Be("AccessCode.AlreadySold");
    }

    [Fact]
    public async Task SellCode_MintedUndistributedCode_Should_Return409Conflict()
    {
        // Arrange: Generate batch but DO NOT distribute
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/minted-sell-test";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "POS Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 1,
                TargetResourceArn = courseArn,
                FaceValue = 50m
            });
        var genResult = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();
        var mintedCode = genResult!.Codes[0];

        // Act: Attempt to sell a minted (undistributed) code
        var sellRes = await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest
        {
            RawCode = mintedCode,
            LibraryId = libraryId
        });

        // Assert: 409 Conflict (AccessCode.InvalidStatus)
        sellRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await sellRes.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("title").GetString().Should().Be("AccessCode.InvalidStatus");
    }

    [Fact]
    public async Task Redeem_SoldCode_Should_Succeed()
    {
        // Arrange: Sell code first
        var tenantId = Guid.NewGuid();
        var (libraryId, _, rawCodes) = await SetupDistributedBatchAsync(tenantId, faceValue: 40m);
        var rawCode = rawCodes[0];

        var sellRes = await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest
        {
            RawCode = rawCode,
            LibraryId = libraryId
        });
        sellRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Student redeems sold code
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest
        {
            RawCode = rawCode
        });

        // Assert: 200 OK and code moves to Redeemed
        redeemRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var codeInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.BatchId != null && c.TenantId == tenantId && c.Status == AccessCodeStatus.Redeemed);
        codeInDb.Status.Should().Be(AccessCodeStatus.Redeemed);
        codeInDb.RedeemedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Redeem_UnsoldCode_Should_Succeed_LaxGate()
    {
        // Arrange: Code is distributed but never sold
        var tenantId = Guid.NewGuid();
        var (_, _, rawCodes) = await SetupDistributedBatchAsync(tenantId, faceValue: 30m);
        var rawCode = rawCodes[0];

        // Act: Student redeems without prior sale (ratified lax gate)
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest
        {
            RawCode = rawCode
        });

        // Assert: 200 OK (no blocking friction) and code moves to Redeemed
        redeemRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var codeInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.BatchId != null && c.TenantId == tenantId && c.Status == AccessCodeStatus.Redeemed);
        codeInDb.Status.Should().Be(AccessCodeStatus.Redeemed);

        // Verify NO sale record exists for this code
        var hasSale = await DbContext.SaleRecords
            .IgnoreQueryFilters()
            .AnyAsync(s => s.AccessCodeId == codeInDb.Id);
        hasSale.Should().BeFalse();
    }
}
