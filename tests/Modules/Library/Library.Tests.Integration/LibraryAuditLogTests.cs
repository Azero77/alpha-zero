using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Library.Application.AccessCodes.SellCode;
using AlphaZero.Modules.Library.Application.AccessCodes.VoidCode;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.SellCode;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Library.Tests.Integration;

public class LibraryAuditLogTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task LifecycleOperations_Should_WriteAuditStreamRows_WithAccurateData()
    {
        // 1. Arrange: Setup Tenant & Library
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/audit-lifecycle";

        var libRes = await Client.PostAsJsonAsync("/library/libraries", new CreateLibraryRequest
        {
            Name = "Lifecycle Audit Lib",
            Address = "Address 1",
            ContactNumber = "123456"
        });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", new AuthorizeResourceRequest
        {
            Id = libraryId,
            ResourceArn = courseArn
        });

        // 2. Mint: Generate a batch of 1 code
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate", new GenerateBatchRequest
        {
            LibraryId = libraryId,
            Quantity = 1,
            TargetResourceArn = courseArn,
            FaceValue = 49.99m
        });
        genRes.EnsureSuccessStatusCode();
        var genData = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();
        var rawCode = genData!.Codes[0];

        // Verify Mint audit row exists
        var codeEntity = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .FirstAsync(x => x.BatchId == genData.BatchId);

        var mintLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.AggregateId == codeEntity.Id && x.Action == "Minted")
            .ToListAsync();

        mintLogs.Should().HaveCount(1);
        mintLogs[0].TenantId.Should().Be(tenantId);
        mintLogs[0].LibraryId.Should().Be(libraryId);
        mintLogs[0].Payload.RootElement.GetProperty("FaceValue").GetDecimal().Should().Be(49.99m);

        // 3. Distribute batch
        var distRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{genData.BatchId}/distribute", new { });
        distRes.EnsureSuccessStatusCode();

        var distLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.AggregateId == codeEntity.Id && x.Action == "Distributed")
            .ToListAsync();

        distLogs.Should().HaveCount(1);
        distLogs[0].TenantId.Should().Be(tenantId);
        distLogs[0].LibraryId.Should().Be(libraryId);

        // 4. Sell code
        var sellRes = await Client.PostAsJsonAsync("/library/access-codes/sell", new SellCodeRequest
        {
            RawCode = rawCode,
            LibraryId = libraryId
        });
        sellRes.EnsureSuccessStatusCode();

        var sellLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.AggregateId == codeEntity.Id && x.Action == "Sold")
            .ToListAsync();

        sellLogs.Should().HaveCount(1);
        sellLogs[0].TenantId.Should().Be(tenantId);
        sellLogs[0].LibraryId.Should().Be(libraryId);
        sellLogs[0].Payload.RootElement.GetProperty("Price").GetDecimal().Should().Be(49.99m);
        sellLogs[0].Payload.RootElement.GetProperty("Currency").GetString().Should().Be("USD");

        // 5. Redeem code
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest
        {
            RawCode = rawCode
        });
        redeemRes.EnsureSuccessStatusCode();

        var redeemLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.AggregateId == codeEntity.Id && x.Action == "Redeemed")
            .ToListAsync();

        redeemLogs.Should().HaveCount(1);
        redeemLogs[0].TenantId.Should().Be(tenantId);
        redeemLogs[0].Payload.RootElement.GetProperty("UnsoldRedemption").GetBoolean().Should().BeFalse();
        redeemLogs[0].Payload.RootElement.TryGetProperty("ForensicLogId", out var forensicProp).Should().BeTrue();
        forensicProp.GetGuid().Should().NotBeEmpty();

        // 6. Void code (post-redemption void)
        var voidRes = await Client.PostAsJsonAsync("/library/access-codes/void", new VoidCodeCommand(rawCode, "Stolen payment"));
        voidRes.EnsureSuccessStatusCode();

        var voidLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.AggregateId == codeEntity.Id && x.Action == "Voided")
            .ToListAsync();

        voidLogs.Should().HaveCount(1);
        voidLogs[0].TenantId.Should().Be(tenantId);
        voidLogs[0].Payload.RootElement.GetProperty("Reason").GetString().Should().Be("Stolen payment");

        // Total stream rows for this code across full lifecycle = 5 (Mint, Distribute, Sell, Redeem, Void)
        var allCodeLogs = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .Where(x => x.AggregateId == codeEntity.Id)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync();

        allCodeLogs.Select(x => x.Action).Should().Equal("Minted", "Distributed", "Sold", "Redeemed", "Voided");
    }

    [Fact]
    public async Task Redeem_UnsoldCode_Should_RecordUnsoldRedemptionTrue_InAuditStream()
    {
        // 1. Arrange: Setup Tenant & Library
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/unsold-audit";

        var libRes = await Client.PostAsJsonAsync("/library/libraries", new CreateLibraryRequest
        {
            Name = "Unsold Audit Lib",
            Address = "Address 1",
            ContactNumber = "123456"
        });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", new AuthorizeResourceRequest
        {
            Id = libraryId,
            ResourceArn = courseArn
        });

        // 2. Generate and distribute (skip sale)
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate", new GenerateBatchRequest
        {
            LibraryId = libraryId,
            Quantity = 1,
            TargetResourceArn = courseArn,
            FaceValue = 25m
        });
        var genData = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();
        var rawCode = genData!.Codes[0];

        await Client.PostAsJsonAsync($"/library/access-codes/batches/{genData.BatchId}/distribute", new { });

        // 3. Redeem directly without prior sale
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest
        {
            RawCode = rawCode
        });
        redeemRes.EnsureSuccessStatusCode();

        // 4. Assert: Audit log records UnsoldRedemption == true
        var codeEntity = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .FirstAsync(x => x.BatchId == genData.BatchId);

        var redeemLog = await DbContext.LibraryAuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.AggregateId == codeEntity.Id && x.Action == "Redeemed");

        redeemLog.Should().NotBeNull();
        redeemLog!.Payload.RootElement.GetProperty("UnsoldRedemption").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Database_Should_Reject_UpdateOrDelete_OnLibraryAuditLogs()
    {
        // 1. Arrange: Insert one audit log
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var codeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var log = LibraryAuditLog.Create(
            eventId,
            tenantId,
            null,
            null,
            "Minted",
            codeId,
            JsonDocument.Parse("{}"));

        DbContext.LibraryAuditLogs.Add(log);
        await DbContext.SaveChangesAsync();

        // 2. Act & Assert: Direct raw SQL UPDATE must throw PostgresException
        var updateAction = async () =>
        {
            await DbContext.Database.ExecuteSqlRawAsync(
                "UPDATE \"Library\".\"LibraryAuditLogs\" SET \"Action\" = 'Tampered' WHERE \"Id\" = {0}",
                log.Id);
        };

        await updateAction.Should().ThrowAsync<PostgresException>()
            .WithMessage("*Updates and deletes are not permitted on LibraryAuditLogs table*");

        // 3. Act & Assert: Direct raw SQL DELETE must throw PostgresException
        var deleteAction = async () =>
        {
            await DbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Library\".\"LibraryAuditLogs\" WHERE \"Id\" = {0}",
                log.Id);
        };

        await deleteAction.Should().ThrowAsync<PostgresException>()
            .WithMessage("*Updates and deletes are not permitted on LibraryAuditLogs table*");
    }

    [Fact]
    public async Task Redelivery_OfDuplicateDomainEvent_Should_Not_Create_DuplicateRow()
    {
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var codeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var repo = new AlphaZero.Modules.Library.Infrastructure.Repositories.LibraryAuditLogRepository(DbContext);

        var log1 = LibraryAuditLog.Create(
            eventId,
            tenantId,
            null,
            null,
            "Minted",
            codeId,
            JsonDocument.Parse("{}"));

        await repo.AddAsync(log1);
        await DbContext.SaveChangesAsync();

        // Duplicate delivery with exact same eventId
        var log2 = LibraryAuditLog.Create(
            eventId,
            tenantId,
            null,
            null,
            "Minted",
            codeId,
            JsonDocument.Parse("{}"));

        // Should return cleanly without throwing and without inserting duplicate
        await repo.AddAsync(log2);
        await DbContext.SaveChangesAsync();

        var count = await repo.GetByAggregateIdAsync(codeId);
        count.Should().HaveCount(1);
    }
}
