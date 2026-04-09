using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using FluentAssertions;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Models;

public class PrincipalTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_Should_Succeed_WithValidUrn()
    {
        // Arrange
        var scope = $"az:courses:{TenantId}:course/*";

        // Act
        var result = Principal.Create(Guid.NewGuid(), UserId.ToString(), PrincipalType.User, TenantId, scope, "Teacher");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.PrincipalScopeUrn.Should().Be(scope);
    }

    [Fact]
    public void Create_Should_Fail_WithInvalidUrnPrefix()
    {
        // Arrange
        var invalidScope = "aws:s3:tenant:path";

        // Act
        var result = Principal.Create(Guid.NewGuid(), UserId.ToString(), PrincipalType.User, TenantId, invalidScope, "Teacher");

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.First().Code.Should().Be("Identity.Application");
    }

    [Fact]
    public void Create_Should_StoreResourceIndex_WhenProvided()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var scope = $"az:courses:{TenantId}:course/{courseId}";

        // Act
        var result = Principal.Create(Guid.NewGuid(), UserId.ToString(), PrincipalType.User, TenantId, scope, "CourseAdmin", courseId, ResourceType.Courses);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.ResourceId.Should().Be(courseId);
        result.Value.ScopeResourceType.Should().Be(ResourceType.Courses);
    }

    [Fact]
    public void AddInlinePolicy_Should_EncapsulateCorrectly()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), UserId.ToString(), PrincipalType.User, TenantId, "az:*:*:*", "Admin").Value;
        var policy = new Policy(Guid.NewGuid(), "Custom", TenantId);

        // Act
        principal.AddInlinePolicy(policy);

        // Assert
        principal.InlinePolicies.Should().HaveCount(1);
        principal.InlinePolicies.Should().Contain(policy);
    }

    [Fact]
    public void RemoveInlinePolicy_Should_WorkById()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), UserId.ToString(), PrincipalType.User, TenantId, "az:*:*:*", "Admin").Value;
        var policyId = Guid.NewGuid();
        var policy = new Policy(policyId, "Custom", TenantId);
        principal.AddInlinePolicy(policy);

        // Act
        principal.RemoveInlinePolicy(policyId);

        // Assert
        principal.InlinePolicies.Should().BeEmpty();
    }
}
