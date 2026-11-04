using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateAdminCode;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;

namespace Library.Tests.Integration;

public class ContractConformanceTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GenerateBatch_Should_Return201WithBatchIdAndCountAndCodes_WhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/contract-test";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "Contract Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        // Act
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 3,
                TargetResourceArn = courseArn,
                FaceValue = 25.50m
            });

        // Assert: 201 Created and rich response shape
        genRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var response = await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>();
        response.Should().NotBeNull();
        response!.BatchId.Should().NotBeEmpty();
        response.GeneratedCount.Should().Be(3);
        response.Codes.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateBatch_Should_Return400_WhenFaceValueIsZeroOrNegative()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/face-val-invalid";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "FaceVal Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        // Act: FaceValue <= 0
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 1,
                TargetResourceArn = courseArn,
                FaceValue = 0m
            });

        // Assert
        genRes.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateBatch_Should_Return400_WhenFaceValueExceeds1000()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/face-val-exceed";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "FaceVal Exceed Lib", Address = "Add", ContactNumber = "123" });
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });

        // Act: FaceValue > 1000
        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 1,
                TargetResourceArn = courseArn,
                FaceValue = 1000.01m
            });

        // Assert
        genRes.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateAdminCode_Should_Return201WithCodeIdAndPlainTextCode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/admin-contract-test";

        // Act
        var genRes = await Client.PostAsJsonAsync("/library/admin/access-codes/generate-single",
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });

        // Assert: 201 Created and response has CodeId and PlainTextCode
        genRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var response = await genRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>();
        response.Should().NotBeNull();
        response!.CodeId.Should().NotBeEmpty();
        response.PlainTextCode.Should().StartWith("ADM-");
    }

    [Fact]
    public async Task Redeem_Should_Return401_WhenUnauthenticated_EvenWithForeignTenantCode()
    {
        // Arrange: mint an admin code on Tenant A
        var tenantA = Guid.NewGuid();
        SetTenant(tenantA);
        var courseArnA = $"az:course:{tenantA}:course/probe-test";

        var genRes = await Client.PostAsJsonAsync("/library/admin/access-codes/generate-single",
            new GenerateAdminCodeRequest { TargetResourceArn = courseArnA });
        genRes.EnsureSuccessStatusCode();
        var rawCode = (await genRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>())!.PlainTextCode;

        // Act: unauthenticated caller sends request scoped to Tenant B (using X-Anonymous)
        var tenantB = Guid.NewGuid();
        var unauthenticatedRequest = new HttpRequestMessage(HttpMethod.Post, "/library/redeem")
        {
            Content = JsonContent.Create(new RedeemCodeRequest { RawCode = rawCode })
        };
        unauthenticatedRequest.Headers.Add("X-TenantId", tenantB.ToString());
        unauthenticatedRequest.Headers.Add("X-Anonymous", "true");

        var redeemRes = await Client.SendAsync(unauthenticatedRequest);

        // Assert: 401 Unauthorized, NOT 403 Forbidden (no tenant boundary probing)
        redeemRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Redeem_Should_Return404_WhenCodeDoesNotExist_ForAuthenticatedUser()
    {
        // Arrange: authenticated caller
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        // Act: redeem a non-existent code
        var redeemRes = await Client.PostAsJsonAsync("/library/redeem",
            new RedeemCodeRequest { RawCode = "NON-EXISTENT-CODE-1234" });

        // Assert: 404 Not Found
        redeemRes.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        var bodyText = await redeemRes.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(bodyText);
        body.RootElement.GetProperty("title").GetString().Should().Be("AccessCode.NotFound");
    }
}
