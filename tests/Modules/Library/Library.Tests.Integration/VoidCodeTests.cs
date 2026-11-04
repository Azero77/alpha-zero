using System.Net.Http.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateAdminCode;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Integration;

public class VoidCodeTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task VoidCode_WithReason_Should_PersistReasonInDatabase()
    {
        // 1. Arrange: mint an admin code (no library needed to void)
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/void-test";

        var genRes = await Client.PostAsJsonAsync("/library/admin/access-codes/generate-single",
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });
        genRes.EnsureSuccessStatusCode();
        var rawCode = (await genRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>())!.PlainTextCode;

        // 2. Act: void with a reason
        var voidRes = await Client.PostAsJsonAsync("/library/access-codes/void",
            new { RawCode = rawCode, Reason = "Stolen by staff" });

        // 3. Assert: success and the reason is persisted on the aggregate row
        voidRes.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var codeInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId);
        codeInDb.Should().NotBeNull();
        codeInDb!.Status.Should().Be(AccessCodeStatus.Voided);
        codeInDb.VoidedReason.Should().Be("Stolen by staff");
    }

    [Fact]
    public async Task VoidCode_WithEmptyReason_Should_Return400_AndNotMutateCode()
    {
        // 1. Arrange: mint an admin code
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/void-empty-reason-test";

        var genRes = await Client.PostAsJsonAsync("/library/admin/access-codes/generate-single",
            new GenerateAdminCodeRequest { TargetResourceArn = courseArn });
        genRes.EnsureSuccessStatusCode();
        var rawCode = (await genRes.Content.ReadFromJsonAsync<GenerateAdminCodeResponse>())!.PlainTextCode;

        // 2. Act: void without a reason
        var voidRes = await Client.PostAsJsonAsync("/library/access-codes/void",
            new { RawCode = rawCode, Reason = "" });

        // 3. Assert: rejected at the validation layer, code untouched
        voidRes.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        var codeInDb = await DbContext.AccessCodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId);
        codeInDb.Should().NotBeNull();
        codeInDb!.Status.Should().Be(AccessCodeStatus.Minted);
        codeInDb.VoidedReason.Should().BeNull();
    }
}
