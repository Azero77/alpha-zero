using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Integration;

public class DistributeBatchTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task DistributeBatch_Should_Return404WithModuleErrorCode_WhenBatchDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var randomBatchId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsJsonAsync($"/library/access-codes/batches/{randomBatchId}/distribute", new { });

        // Assert: 404 with the module's not-found error code - a nonexistent batch must
        // never look like a successful distribution.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        var bodyText = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(bodyText);
        body.RootElement.GetProperty("title").GetString().Should().Be("Batch.NotFound");
    }

    [Fact]
    public async Task DistributeBatch_Should_Succeed_And_PersistDistributedStatus_WhenBatchExists()
    {
        // Arrange: full flow to mint a real batch of access codes
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/distribute-test";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "Distribute Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch.GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 3,
                TargetResourceArn = courseArn,
            });
        genRes.EnsureSuccessStatusCode();

        // The generated batch id comes from the persisted codes (no tracking: asserts must
        // observe the database, not the identity-map snapshot read before distribution)
        var batchId = (await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.TenantId == tenantId)).BatchId!.Value;

        // Act
        var distributeRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/distribute", new { });

        // Assert: success AND the batch state actually changed in the database
        distributeRes.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var codesInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .ToListAsync();
        codesInDb.Should().HaveCount(3);
        codesInDb.Should().OnlyContain(x => x.Status == AlphaZero.Modules.Library.Domain.AccessCodeStatus.Distributed);
    }
}
