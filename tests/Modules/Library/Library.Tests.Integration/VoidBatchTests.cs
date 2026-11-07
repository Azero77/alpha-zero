using System.Net.Http.Json;
using AlphaZero.Modules.Library.Application.AccessCodes.SellCode;
using AlphaZero.Modules.Library.Application.AccessCodes.VoidCode;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.SellCode;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.VoidBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Library.Tests.Integration;

public class VoidBatchTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task VoidBatch_MixedBatch_Should_VoidVoidableCodes_AndSkipTerminalCodes()
    {
        // 1. Arrange: Setup Tenant & Library
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/void-batch-test";

        var libRes = await Client.PostAsJsonAsync("/library/libraries", new CreateLibraryRequest
        {
            Name = "Recall Library",
            Address = "Central 101",
            ContactNumber = "555-1234"
        });
        libRes.EnsureSuccessStatusCode();
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", new AuthorizeResourceRequest
        {
            Id = libraryId,
            ResourceArn = courseArn
        });

        // 2. Generate a batch of 4 codes
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate", new GenerateBatchRequest
        {
            LibraryId = libraryId,
            Quantity = 4,
            TargetResourceArn = courseArn,
            FaceValue = 60m
        });
        genRes.EnsureSuccessStatusCode();
        var genData = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();
        var batchId = genData!.BatchId;
        var codes = genData.Codes;

        // 3. Distribute the batch
        var distRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/distribute", new { });
        distRes.EnsureSuccessStatusCode();

        // 4. Set up states:
        // Code 0: Sold
        var sellRes = await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest
        {
            RawCode = codes[0],
            LibraryId = libraryId
        });
        sellRes.EnsureSuccessStatusCode();

        // Code 1: Redeemed
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest
        {
            RawCode = codes[1]
        });
        redeemRes.EnsureSuccessStatusCode();

        // Code 2: Pre-voided
        var voidRes = await Client.PostAsJsonAsync("/library/access-codes/void", new VoidCodeCommand(codes[2], "Pre-voided manually"));
        voidRes.EnsureSuccessStatusCode();

        // Code 3: Remains Distributed

        // Baseline: Check audit log rows before batch recall
        var preRecallVoidLogsCount = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .CountAsync(x => x.Action == "Voided" && x.TenantId == tenantId);

        preRecallVoidLogsCount.Should().Be(1); // from Code 2 pre-void

        // 5. Act: Bulk Recall the batch
        var recallRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/void", new VoidBatchRequest
        {
            BatchId = batchId,
            Reason = "Stolen physical card stock"
        });

        // 6. Assert Response: 200 OK
        recallRes.EnsureSuccessStatusCode();
        var recallResult = await recallRes.Content.ReadFromJsonAsync<VoidBatchResponse>();

        recallResult.Should().NotBeNull();
        recallResult!.BatchId.Should().Be(batchId);
        recallResult.TotalCodes.Should().Be(4);
        recallResult.VoidedCount.Should().Be(3); // Code 0 (Sold), Code 1 (Redeemed), Code 3 (Distributed)
        recallResult.SkippedCount.Should().Be(1); // Code 2 (already Voided)
        recallResult.VoidedCodeIds.Should().HaveCount(3);

        // Assert Database: All 4 codes are now Voided
        var codesInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .Where(x => x.BatchId == batchId)
            .ToListAsync();

        codesInDb.Should().HaveCount(4);
        codesInDb.Should().OnlyContain(x => x.Status == AccessCodeStatus.Voided);

        // Assert Audit Stream: Exactly 3 new void audit rows were produced, each with reason
        var postRecallVoidLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.Action == "Voided" && x.TenantId == tenantId)
            .ToListAsync();

        postRecallVoidLogs.Should().HaveCount(4); // 1 pre-existing + 3 from recall
        var recallLogs = postRecallVoidLogs
            .Where(x => x.Payload.RootElement.GetProperty("Reason").GetString() == "Stolen physical card stock")
            .ToList();

        recallLogs.Should().HaveCount(3);

        // 7. Re-run: Idempotency assertion
        var reRunRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/void", new VoidBatchRequest
        {
            BatchId = batchId,
            Reason = "Duplicate recall attempt"
        });

        reRunRes.EnsureSuccessStatusCode();
        var reRunResult = await reRunRes.Content.ReadFromJsonAsync<VoidBatchResponse>();

        reRunResult.Should().NotBeNull();
        reRunResult!.TotalCodes.Should().Be(4);
        reRunResult.VoidedCount.Should().Be(0);
        reRunResult.SkippedCount.Should().Be(4);
        reRunResult.VoidedCodeIds.Should().BeEmpty();

        // No new audit rows produced
        var finalVoidLogsCount = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .CountAsync(x => x.Action == "Voided" && x.TenantId == tenantId);

        finalVoidLogsCount.Should().Be(4);
    }

    [Fact]
    public async Task VoidBatch_EmptyReason_Should_Return400BadRequest()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var batchId = Guid.NewGuid();

        var res = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/void", new VoidBatchRequest
        {
            BatchId = batchId,
            Reason = "   "
        });

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VoidBatch_NonExistentBatch_Should_Return404NotFound()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var batchId = Guid.NewGuid();

        var res = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/void", new VoidBatchRequest
        {
            BatchId = batchId,
            Reason = "Batch does not exist"
        });

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
