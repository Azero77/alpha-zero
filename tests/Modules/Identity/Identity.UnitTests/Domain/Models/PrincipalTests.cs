using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Models;

public class PrincipalTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string Username = "iam-user-123";
    private static readonly string PasswordHash = "hashed-password";

    [Fact]
    public void Create_Should_Succeed_WithValidUrn()
    {
        // Arrange
        var scope = $"az:courses:{TenantId}:course/101";

        // Act
        var result = Principal.Create(Guid.NewGuid(), Username, PrincipalType.User, TenantId, scope, "Test Principal", PasswordHash, Guid.NewGuid(), ResourceType.Courses);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.PrincipalScopeUrn.Should().Be(scope.ToLowerInvariant());
    }

    [Fact]
    public void Create_Should_Fail_WithInvalidUrn()
    {
        // Arrange
        var invalidScope = "not-an-arn";

        // Act
        var result = Principal.Create(Guid.NewGuid(), Username, PrincipalType.User, TenantId, invalidScope, "Test Principal", PasswordHash, Guid.NewGuid(), ResourceType.Courses);

        // Assert
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void AddInlinePolicy_Should_EncapsulateCorrectly()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), Username, PrincipalType.User, TenantId, null, "Custom", PasswordHash).Value;
        var policy = new Policy(Guid.NewGuid(), "Custom", TenantId);

        // Act
        principal.AddInlinePolicy(policy);

        // Assert
        principal.InlinePolicies.Should().HaveCount(1);
        principal.InlinePolicies.Should().Contain(policy);
    }
}
