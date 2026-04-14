using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using AlphaZero.Shared.Domain;

namespace Identity.Tests.Integration;

public class IdentityTests : BaseIntegrationTest
{
    public IdentityTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SavePrincipal_ShouldPersistInlinePoliciesAsJsonB()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var principal = Principal.Create(Guid.NewGuid(), "cognito-sub", PrincipalType.User, tenantId, "az:*:*:*", "Test User").Value;
        
        var policy = new Policy(Guid.NewGuid(), "InlinePolicy", tenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "courses:Edit" }, true, new() { ResourcePattern.All}));
        principal.AddInlinePolicy(policy);

        // Act
        DbContext.Principals.Add(principal);
        await DbContext.SaveChangesAsync();

        // Assert
        var saved = await DbContext.Principals
            .Include(p => p.InlinePolicies)
            .FirstAsync(p => p.Id == principal.Id);

        saved.InlinePolicies.Should().HaveCount(1);
        saved.InlinePolicies.First().Statements.First().Actions.Should().Contain("courses:Edit");
    }

    [Fact]
    public async Task Authorize_ShouldWorkEndToEnd_WithManagedPolicyAssignments()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var principal = Principal.Create(Guid.NewGuid(), "cognito-sub", PrincipalType.User, tenantId, "az:courses:*:*", "Teacher").Value;
        
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "CourseAdmin", new() { new PolicyTemplateStatement("S1", new() { "courses:Publish" }, true) });

        // Resolve services from the SAME scope
        var managedRepo = Resolve<IManagedPolicyRepository>();
        var evaluator = Resolve<PolicyEvaluatorService>();

        // Persist base entities
        DbContext.Principals.Add(principal);
        DbContext.ManagedPolicies.Add(managedPolicy);
        await DbContext.SaveChangesAsync();

        // Act: Assign via Repository
        await managedRepo.AssignPolicyToPrincipal(principal.Id, managedPolicy.Id);
        await DbContext.SaveChangesAsync();

        // Act: Evaluate
        var result = await evaluator.Authorize(principal.Id, tenantId, "course/1", ResourceType.Courses, "courses:Publish");

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task PrincipalIndexing_Should_ReturnCorrectPrincipalsByResource()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        
        var p1 = Principal.Create(Guid.NewGuid(), "u1", PrincipalType.User, tenantId, "az:*:*:*", "User 1", courseId, ResourceType.Courses).Value;
        var p2 = Principal.Create(Guid.NewGuid(), "u2", PrincipalType.User, tenantId, "az:*:*:*", "User 2", courseId, ResourceType.Courses).Value;
        var p3 = Principal.Create(Guid.NewGuid(), "u3", PrincipalType.User, tenantId, "az:*:*:*", "User 3", Guid.NewGuid(), ResourceType.Courses).Value;

        DbContext.Principals.AddRange(p1, p2, p3);
        await DbContext.SaveChangesAsync();

        // Act: Resolve repo from the same scope
        var repo = Resolve<IPrincipalRepository>();
        var results = await repo.GetPrincipalsByResourceAsync(courseId, ResourceType.Courses);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(p => p.Name == "User 1");
        results.Should().Contain(p => p.Name == "User 2");
    }
}
