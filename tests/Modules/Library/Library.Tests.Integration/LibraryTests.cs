using System.Net.Http.Json;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Integration;

public class LibraryTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task LibraryWorkflow_Should_Succeed_FromCreationToRedemption()
    {
        // 1. Arrange: Setup Tenant
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:courses:{tenantId}:course/math-101";

        // 2. Act: Create Library
        var createReq = new CreateLibraryRequest 
        { 
            Name = "Damascus Central", 
            Address = "Main St", 
            ContactNumber = "0933" 
        };
        var createRes = await Client.PostAsJsonAsync("/library/libraries", createReq);
        createRes.EnsureSuccessStatusCode();
        var libraryId = (await createRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        // 3. Act: Authorize Course for Library
        var authReq = new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn };
        var authRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources", authReq);
        authRes.EnsureSuccessStatusCode();

        // 4. Act: Generate Access Codes
        var genReq = new GenerateBatchRequest 
        { 
            LibraryId = libraryId, 
            Quantity = 1, 
            TargetResourceArn = courseArn 
        };
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate", genReq);
        genRes.EnsureSuccessStatusCode();
        var rawCode = (await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>())!.Codes[0];

        // 5. Act: Redeem Code
        var redeemReq = new RedeemCodeRequest { RawCode = rawCode };
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem", redeemReq);
        redeemRes.EnsureSuccessStatusCode();

        // 6. Assert: Verify DB state
        var codeInDb = await DbContext.AccessCodes.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId);
        codeInDb.Should().NotBeNull();
        codeInDb!.Status.ToString().Should().Be("Redeemed");
        codeInDb.TargetResourceArn.Value.Should().Be(courseArn.ToLowerInvariant());
    }

    [Fact]
    public async Task CrossTenant_LibraryAccess_Should_BeIsolated()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        SetTenant(tenantA);
        var libraryA = AlphaZero.Modules.Library.Domain.Library.Create("Lib A", "Add", "123", tenantA);
        DbContext.Libraries.Add(libraryA);
        await DbContext.SaveChangesAsync();

        // Act & Assert: Tenant B tries to get Tenant A's library
        SetTenant(tenantB);
        var response = await Client.GetAsync($"/library/libraries/{libraryA.Id}");
        
        // Should be NotFound because of Global Filters on TenantId
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}