using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
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
        var result = Principal.Create(Guid.NewGuid(), Username, PasswordHash, "Test Principal", PrincipalType.User, scope, TenantId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.PrincipalScope?.Value.Should().Be(scope.ToLowerInvariant());
    }

    [Fact]
    public void Create_Should_Fail_WithInvalidUrn()
    {
        // Arrange
        var invalidScope = "not-an-arn";

        // Act
        var result = Principal.Create(Guid.NewGuid(), Username, PasswordHash, "Test Principal", PrincipalType.User, invalidScope, TenantId);

        // Assert
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void AddPolicy_Should_EncapsulateCorrectly()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), Username, PasswordHash, "Custom", PrincipalType.User, null, TenantId).Value;
        var policy = new InlinePolicy(Guid.NewGuid(), "Custom", TenantId);

        // Act
        principal.AddPolicy(policy);

        // Assert
        principal.Policies.Should().HaveCount(1);
        principal.Policies.Should().Contain(policy);
    }
}
