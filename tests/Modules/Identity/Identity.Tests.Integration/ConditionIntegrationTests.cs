using System.Net.Http.Json;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using System.Text.Json;
using ErrorOr;

namespace Identity.Tests.Integration;

public class ConditionIntegrationTests : BaseIntegrationTest
{
    public ConditionIntegrationTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Authorize_Should_RespectConditions_StoredInDb()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        // 1. Create a Managed Policy with a Condition (AndNode)
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/math-101\"").RootElement);
        var c2 = new ConditionNode("RequiredPermission", Operator.StringEquals, JsonDocument.Parse("\"courses:View\"").RootElement);
        var condition = new AndNode(new List<IConditionNode> { c1, c2 });

        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "ConditionalPolicy", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "courses:View" }, true, condition) 
        });

        var user = TenantUser.Create(tenantId, "ali-sub", "Ali").Value;
        var principal = Principal.Create(Guid.NewGuid(), "student-role", "hash", "Student", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);

        var principalRepo = Resolve<IPrincipalRepository>();
        var managedPolicyRepo = Resolve<IManagedPolicyRepository>();

        managedPolicyRepo.Add(managedPolicy);
        await DbContext.SaveChangesAsync();

        principalRepo.Add(principal);
        await DbContext.SaveChangesAsync();

        DbContext.TenantUsers.Add(user);
        await DbContext.SaveChangesAsync();

        var assignment = TenantUserPrinciaplAssignment.Create(tenantId, user, principal, $"az:course:{tenantId}:course/math-101").Value;
        DbContext.TenantPrinciaplAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        var evaluator = Resolve<IPolicyEvaluatorService>();

        // 2. Act: Evaluate with matching condition
        var resultMatch = await evaluator.Authorize(new AuthorizationContext()
        {
            Id = user.Id,
            TenantId = tenantId,
            ResourcePath = "course/math-101",
            ResourceType = "course",
            RequiredPermission = "courses:View",
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString()
        });

        // 3. Act: Evaluate with failing condition (wrong resource path)
        var resultFail = await evaluator.Authorize(new AuthorizationContext()
        {
            Id = user.Id,
            TenantId = tenantId,
            ResourcePath = "course/history-101",
            ResourceType = "course",
            RequiredPermission = "courses:View",
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString()
        });

        // Assert
        resultMatch.IsError.Should().BeFalse("Match should succeed");
        resultFail.IsError.Should().BeTrue("Fail should be forbidden due to condition");
        resultFail.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }
}
