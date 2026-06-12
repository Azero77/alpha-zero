using System.Net.Http.Json;
using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
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
        
        var principal = Principal.Create(Guid.NewGuid(), "student-role", "hash", "Student", PrincipalType.Role, null, tenantId).Value;
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "StudentBase", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "courses:View" }, true) 
        });
        principal.AddPolicy(managedPolicy);

        // Save everything
        DbContext.TenantUsers.Add(user);
        // Note: In integration tests we'd need to save via repository if we used DataModels, 
        // but for now we assume DbContext handles it for simplicity or we update it.
        // Actually, DbContext now has DbSet<PrincipalDataModel>, so we must use that or the Repository.
        
        var principalRepo = Resolve<IPrincipalRepository>();
        var managedPolicyRepo = Resolve<IManagedPolicyRepository>();

        managedPolicyRepo.Add(managedPolicy);
        await DbContext.SaveChangesAsync();

        principalRepo.Add(principal);
        await DbContext.SaveChangesAsync();

        // Create Assignment (Enrollment)
        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:course:{tenantId}:course/math-101").Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        // Act: Evaluate via the Service
        var evaluator = Resolve<IPolicyEvaluatorService>();
        var context = new AuthorizationContext
        {
            Id = user.Id,
            TenantId = tenantId,
            RequiredPermission = "courses:View",
            ResourcePath = $"course/math-101",
            ResourceType = "course",
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString()
        };
        var result = await evaluator.Authorize(context);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Authorize_Principal_Should_WorkWithInlinePolicies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var hasher = Resolve<IPasswordHasher>();
        var passwordHash = hasher.HashPassword("secure-password");

        var user = TenantUser.Create(tenantId, "ali-sub", "Ali").Value;

        var principal = Principal.Create(Guid.NewGuid(), "ali-principal", passwordHash, "Custom", PrincipalType.User, "az:*:*:*", tenantId).Value;
        var policy = new InlinePolicy(Guid.NewGuid(), "Inline", tenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "video:Stream" }, true, new() { ResourcePattern.All }));
        principal.AddPolicy(policy);

        DbContext.TenantUsers.Add(user);
        var principalRepo = Resolve<IPrincipalRepository>();
        principalRepo.Add(principal);
        await DbContext.SaveChangesAsync();

        // Act
        var evaluator = Resolve<IPolicyEvaluatorService>();
        var context = new AuthorizationContext
        {
            Id = principal.Id,
            TenantId = tenantId,
            RequiredPermission = "video:Stream",
            ResourcePath = "video/1",
            ResourceType = "video",
            AuthenticationMethod = AuthenticationMethod.Principal.ToString()
        };
        var result = await evaluator.Authorize(context);

        // Assert
        result.IsError.Should().BeFalse();
    }
}
