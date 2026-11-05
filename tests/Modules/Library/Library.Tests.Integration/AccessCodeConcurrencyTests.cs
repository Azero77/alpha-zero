using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Modules.Library.Presentation;
using AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;
using AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;
using AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;
using AlphaZero.Shared.Domain;
using Autofac;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Tests.Integration;

public class AccessCodeConcurrencyTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<(Guid LibraryId, string RawCode, Guid BatchId)> SetupDistributedCodeAsync(Guid tenantId)
    {
        SetTenant(tenantId);
        var courseArn = $"az:course:{tenantId}:course/race-{Guid.NewGuid():N}";

        var libRes = await Client.PostAsJsonAsync("/library/libraries",
            new CreateLibraryRequest { Name = "Race Lib", Address = "Main St", ContactNumber = "123" });
        libRes.EnsureSuccessStatusCode();
        var libraryId = (await libRes.Content.ReadFromJsonAsync<CreateLibraryResponse>())!.Id;

        var authRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/resources",
            new AuthorizeResourceRequest { Id = libraryId, ResourceArn = courseArn });
        authRes.EnsureSuccessStatusCode();

        var genRes = await Client.PostAsJsonAsync($"/library/libraries/{libraryId}/access-codes/generate",
            new GenerateBatchRequest
            {
                LibraryId = libraryId,
                Quantity = 1,
                TargetResourceArn = courseArn,
                FaceValue = 50m
            });
        genRes.EnsureSuccessStatusCode();
        var genData = (await genRes.Content.ReadFromJsonAsync<GenerateBatchResponse>())!;
        var rawCode = genData.Codes[0];
        var batchId = genData.BatchId;

        var distRes = await Client.PostAsJsonAsync($"/library/access-codes/batches/{batchId}/distribute", new { });
        distRes.EnsureSuccessStatusCode();

        return (libraryId, rawCode, batchId);
    }

    [Fact]
    public async Task ConcurrentRedeem_ByDifferentUsers_ShouldResultInExactlyOneWinner_AndOneConflict()
    {
        // Run repeated iterations to guarantee race condition determinism under concurrency (#54)
        const int iterations = 5;

        for (int i = 0; i < iterations; i++)
        {
            var tenantId = Guid.NewGuid();
            var (_, rawCode, _) = await SetupDistributedCodeAsync(tenantId);

            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();

            var client1 = Factory.CreateClient();
            client1.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());
            client1.DefaultRequestHeaders.Add("X-Test-User-Id", user1.ToString());

            var client2 = Factory.CreateClient();
            client2.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());
            client2.DefaultRequestHeaders.Add("X-Test-User-Id", user2.ToString());

            var req1 = new RedeemCodeRequest { RawCode = rawCode };
            var req2 = new RedeemCodeRequest { RawCode = rawCode };

            // Act: Race two redeem requests concurrently
            var task1 = client1.PostAsJsonAsync("/library/redeem", req1);
            var task2 = client2.PostAsJsonAsync("/library/redeem", req2);

            var responses = await Task.WhenAll(task1, task2);

            var statusCodes = responses.Select(r => r.StatusCode).ToList();

            // Assert: Exactly one 200 OK and exactly one 409 Conflict
            statusCodes.Should().ContainSingle(s => s == HttpStatusCode.OK,
                $"Iteration {i}: exactly one user must successfully redeem");
            statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Conflict,
                $"Iteration {i}: the losing concurrent user must receive 409 Conflict");

            var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            var conflictBody = await conflictResponse.Content.ReadAsStringAsync();
            var problem = JsonDocument.Parse(conflictBody);
            problem.RootElement.GetProperty("title").GetString().Should().Be("AccessCode.InvalidStatus");

            // Verify in DB that code is Redeemed by the winner
            var winningResponse = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
            var winnerUserId = (winningResponse == responses[0]) ? user1 : user2;

            var codeInDb = await DbContext.AccessCodes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId);

            codeInDb.Should().NotBeNull();
            codeInDb!.Status.Should().Be(AccessCodeStatus.Redeemed);
            codeInDb.RedeemedByUserId.Should().Be(winnerUserId);
        }
    }

    [Fact]
    public async Task ConcurrentVoid_ByDifferentCallers_ShouldResultInExactlyOneWinner_AndOneConflict()
    {
        // Run repeated iterations to verify concurrent void behavior
        const int iterations = 5;

        for (int i = 0; i < iterations; i++)
        {
            var tenantId = Guid.NewGuid();
            var (_, rawCode, _) = await SetupDistributedCodeAsync(tenantId);

            var client1 = Factory.CreateClient();
            client1.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());

            var client2 = Factory.CreateClient();
            client2.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());

            var voidPayload1 = new { RawCode = rawCode, Reason = "Caller 1 void" };
            var voidPayload2 = new { RawCode = rawCode, Reason = "Caller 2 void" };

            // Act: Race two void requests concurrently
            var task1 = client1.PostAsJsonAsync("/library/access-codes/void", voidPayload1);
            var task2 = client2.PostAsJsonAsync("/library/access-codes/void", voidPayload2);

            var responses = await Task.WhenAll(task1, task2);

            var statusCodes = responses.Select(r => r.StatusCode).ToList();

            // Assert: Exactly one 204 NoContent and exactly one 409 Conflict (AccessCode.InvalidStatus)
            statusCodes.Should().ContainSingle(s => s == HttpStatusCode.NoContent,
                $"Iteration {i}: exactly one void must succeed");
            statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Conflict,
                $"Iteration {i}: the other void must receive 409 Conflict");

            var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            var conflictBody = await conflictResponse.Content.ReadAsStringAsync();
            var problem = JsonDocument.Parse(conflictBody);
            problem.RootElement.GetProperty("title").GetString().Should().Be("AccessCode.AlreadyVoided");

            // Verify in DB that code is Voided
            var codeInDb = await DbContext.AccessCodes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId);

            codeInDb.Should().NotBeNull();
            codeInDb!.Status.Should().Be(AccessCodeStatus.Voided);
            codeInDb.VoidedReason.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task UpdateTripwire_ShouldReturnConflict_WhenDatabaseStatusChangedConcurrently()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (_, rawCode, _) = await SetupDistributedCodeAsync(tenantId);

        var module = Resolve<LibraryModule>();
        using var moduleScope = module.CreateScope();
        var scopedContext = moduleScope.Resolve<AppDbContext>();
        var scopedRepo = moduleScope.Resolve<IAccessCodeRepository>();
        var passwordHasher = moduleScope.Resolve<IPasswordHasher>();

        var hash = passwordHasher.HashPassword(rawCode);

        // Begin transaction to satisfy FOR UPDATE requirement
        await using var tx = await scopedContext.Database.BeginTransactionAsync();

        // 1. Load aggregate with FOR UPDATE (status = Distributed)
        var code = await scopedRepo.GetByHashForUpdateAsync(hash);
        code.Should().NotBeNull();
        code!.Status.Should().Be(AccessCodeStatus.Distributed);

        // 2. Simulate out-of-band / concurrent update modifying the status in DB directly behind the aggregate's back
        await scopedContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Library\".\"AccessCodes\" SET \"Status\" = 'Voided' WHERE \"Id\" = {code.Id}");

        // 3. Aggregate transitions in-memory from Distributed -> Redeemed (setting ExpectedPreviousStatus = Distributed)
        var redeemResult = code.Redeem(Guid.NewGuid());
        redeemResult.IsError.Should().BeFalse();
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Distributed);

        // 4. Act: Attempt repository UpdateAsync (conditional update WHERE status = expectedStatus)
        var updateResult = await scopedRepo.UpdateAsync(code);

        // 5. Assert: Tripwire fires because 0 rows matched (status was Voided in DB, not Distributed)
        updateResult.IsError.Should().BeTrue();
        updateResult.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        updateResult.FirstError.Description.Should().Contain("Tripwire fired");

        await tx.RollbackAsync();
    }
}
