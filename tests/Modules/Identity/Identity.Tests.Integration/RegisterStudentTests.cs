using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Presentation.Auth.Commands.RegisterStudent;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.Tests.Integration;

public class RegisterStudentTests : BaseIntegrationTest
{
    public RegisterStudentTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterStudent_ShouldCreatePrincipalAndAttachPolicy_WhenRequestIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        // Seed the StudentAccess policy
        var studentPolicy = new ManagedPolicy(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            "StudentAccess",
            new()
            {
                new ManagedPolicyStatement("AllowStudentBase", new() { "courses:View", "video:Stream" }, true)
            });
        DbContext.ManagedPolicies.Add(studentPolicy);
        await DbContext.SaveChangesAsync();

        var request = new RegisterStudentRequest
        {
            TenantId = tenantId,
            Username = "teststudent",
            Password = "securePassword123",
            Name = "Test Student"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/identity/auth/register-student", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegisterStudentResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();

        // Verify in database
        var principalModel = await DbContext.Principals
            .Include(p => p.ManagedPolicies)
            .FirstOrDefaultAsync(p => p.Id == result.Id);
            
        principalModel.Should().NotBeNull();
        principalModel!.Username.Should().Be("teststudent");
        principalModel.TenantId.Should().Be(tenantId);
        principalModel.Name.Should().Be("Test Student");
        principalModel.ManagedPolicies.Should().ContainSingle(p => p.Name == "StudentAccess");
    }

    [Fact]
    public async Task RegisterStudent_ShouldReturnConflict_WhenUsernameAlreadyExistsInTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        // Seed the StudentAccess policy
        var studentPolicy = new ManagedPolicy(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            "StudentAccess",
            new()
            {
                new ManagedPolicyStatement("AllowStudentBase", new() { "courses:View", "video:Stream" }, true)
            });
        DbContext.ManagedPolicies.Add(studentPolicy);

        // Create an existing principal with the same username
        var existingPrincipal = new AlphaZero.Modules.Identity.Infrastructure.Models.PrincipalDataModel
        {
            Id = Guid.NewGuid(),
            Username = "duplicatestudent",
            PasswordHash = "somehash",
            Name = "Existing Student",
            PrincipalType = AlphaZero.Modules.Identity.Domain.Models.Principals.PrincipalType.User,
            TenantId = tenantId
        };
        DbContext.Principals.Add(existingPrincipal);
        await DbContext.SaveChangesAsync();

        var request = new RegisterStudentRequest
        {
            TenantId = tenantId,
            Username = "duplicatestudent",
            Password = "securePassword123",
            Name = "Duplicate Student"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/identity/auth/register-student", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RegisterStudent_ShouldReturnValidationError_WhenPasswordIsTooShort()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var request = new RegisterStudentRequest
        {
            TenantId = tenantId,
            Username = "shortpasswordstudent",
            Password = "short", // less than 8 chars
            Name = "Short Password Student"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/identity/auth/register-student", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
