using System.Net.Http.Json;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using AlphaZero.Shared.Queries;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Integration;

public class RedemptionAuditLogTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task RedeemingCode_Should_Create_AuditLog()
    {
        // 1. Arrange: Setup Tenant & Library
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/audit-test";

        var libRes = await Client.PostAsJsonAsync("/library/libraries", new CreateLibraryRequest { Name = "Audit Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate", new GenerateBatchRequest { LibraryId = libraryId, Quantity = 1, TargetResourceArn = courseArn });
        var rawCode = (await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>())!.Codes[0];

        // 2. Act: Redeem with IP and Fingerprint
        var redeemReq = new RedeemCodeRequest 
        { 
            RawCode = rawCode,
        };
        Client.DefaultRequestHeaders.Add("X-Device-Id", "test-device-123");
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", redeemReq);
        redeemRes.EnsureSuccessStatusCode();

        // 3. Assert: Audit log exists in DB
        var log = await DbContext.RedemptionAuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId);

        log.Should().NotBeNull();
        log!.LibraryId.Should().Be(libraryId);
        //log.DeviceFingerprint.Should().Be("test-device-123");
        log.StrategyId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAuditLogs_Should_Return_Logs_For_Library()
    {
        // 1. Arrange: Setup Tenant, Library and a Redeemed Code
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/query-test";

        var libRes = await Client.PostAsJsonAsync("/library/libraries", new CreateLibraryRequest { Name = "Query Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate", new GenerateBatchRequest { LibraryId = libraryId, Quantity = 1, TargetResourceArn = courseArn });
        var rawCode = (await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>())!.Codes[0];

        await Client.PostAsJsonAsync("/library/redeem", new RedeemCodeRequest { RawCode = rawCode });

        // 2. Act: Query logs
        var queryRes = await Client.GetAsync($"/library/libraries/{libraryId}/audit-logs");
        queryRes.EnsureSuccessStatusCode();

        var logs = await queryRes.Content.ReadFromJsonAsync<PagedResult<RedemptionAuditLogDto>>();

        // 3. Assert
        logs.Should().NotBeNull();
        logs!.Items.Should().HaveCount(1);
        logs.Items.First().LibraryId.Should().Be(libraryId);
    }
}
