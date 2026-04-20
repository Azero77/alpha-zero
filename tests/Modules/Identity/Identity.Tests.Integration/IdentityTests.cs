using System.Net.Http.Json;
using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Modules.Identity.Presentation.Auth.Commands.LoginPrincipal;
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
        SetTenant(tenantId);
        
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
    public async Task Authorize_Principal_Should_WorkWithJsonBInlinePolicies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var hasher = Resolve<IPasswordHasher>();
        var passwordHash = hasher.HashPassword("secure-password");

        var user = TenantUser.Create(tenantId, "ali-sub", "Ali").Value;

        var principal = Principal.Create(Guid.NewGuid(), "ali-principal", PrincipalType.User, tenantId, "az:*:*:*", "Custom", passwordHash).Value;
        var policy = new Policy(Guid.NewGuid(), "Inline", tenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "video:Stream" }, true, new() { ResourcePattern.All }));
        principal.AddInlinePolicy(policy);

        DbContext.TenantUsers.Add(user);
        DbContext.Principals.Add(principal);
        await DbContext.SaveChangesAsync();

        // Act
        var evaluator = Resolve<IPolicyEvaluatorService>();
        var result = await evaluator.Authorize(
            principal.Id, 
            tenantId, 
            "video/1", 
            ResourceType.Videos, 
            "video:Stream", 
            AuthorizationMethod.Principal.ToString());

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task LoginPrincipal_Should_ReturnToken_WhenCredentialsAreValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var hasher = Resolve<IPasswordHasher>();
        var passwordHash = hasher.HashPassword("secure-password");

        var principal = Principal.Create(Guid.NewGuid(), "iam-user", PrincipalType.User, tenantId, null, "IAM User", passwordHash).Value;
        DbContext.Principals.Add(principal);
        await DbContext.SaveChangesAsync();

        var request = new LoginPrincipalRequest 
        { 
            TenantId = tenantId, 
            Username = "iam-user", 
            Password = "secure-password" 
        };

        // Act
        var response = await Client.PostAsJsonAsync("/identity/auth/login-principal", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeEmpty();
        result.TenantUserId.Should().Be(principal.Id);
    }
}
