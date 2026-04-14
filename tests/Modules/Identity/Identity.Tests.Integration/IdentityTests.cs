using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Tests.Integration;

public class IdentityTests : BaseIntegrationTest
{
    public IdentityTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Authorize_TenantUser_ShouldWorkEndToEnd_WithScopedAssignments()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "ali-sub", "Ali").Value;
        
        // Use a template for the "Student" role
        var template = new PrincipalTemplate(Guid.NewGuid(), "Student", PrincipalType.Role);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "StudentBase", new() 
        { 
            new PolicyTemplateStatement("S1", new() { "courses:View" }, true) 
        });
        template.ManagedPolicies.Add(managedPolicy);

        // Save everything
        DbContext.TenantUsers.Add(user);
        DbContext.PrincipalTemplates.Add(template);
        DbContext.ManagedPolicies.Add(managedPolicy);
        await DbContext.SaveChangesAsync();

        // Create Assignment (Enrollment)
        var assignment = TenantUserPrinciaplAssignment.Create(tenantId, user, template, $"az:courses:{tenantId}:course/math-101").Value;
        DbContext.TenantPrinciaplAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        // Act: Evaluate via the Service
        var evaluator = Resolve<IPolicyEvaluatorService>();
        var result = await evaluator.Authorize(
            user.Id, 
            tenantId, 
            "course/math-101", 
            ResourceType.Courses, 
            "courses:View", 
            AuthorizationMethod.TenantUser.ToString(), 
            user.ActiveSessionId);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Authorize_Principal_ShouldWorkWithJsonBInlinePolicies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "ali-sub", "Ali").Value;
        
        var principal = Principal.Create(Guid.NewGuid(), user.IdentityId, PrincipalType.User, tenantId, "az:*:*:*", "Custom").Value;
        var policy = new Policy(Guid.NewGuid(), "Inline", tenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "admin:Access" }, true, new() { ResourcePattern.All }));
        principal.AddInlinePolicy(policy);

        DbContext.TenantUsers.Add(user);
        DbContext.Principals.Add(principal);
        await DbContext.SaveChangesAsync();

        // Act
        var evaluator = Resolve<IPolicyEvaluatorService>();
        var result = await evaluator.Authorize(
            principal.Id, 
            tenantId, 
            "dashboard", 
            ResourceType.Users, 
            "admin:Access", 
            AuthorizationMethod.Principal.ToString());

        // Assert
        result.IsError.Should().BeFalse();
    }
}
