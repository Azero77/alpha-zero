using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Models;

public class ResourceArnTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    [Fact]
    public void Create_Should_Succeed_ForValidStrictArn()
    {
        // Act
        var result = ResourceArn.Create("courses", TenantId.ToString(), $"course/{CourseId}");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.ToString().Should().Be($"az:courses:{TenantId.ToString().ToLowerInvariant()}:course/{CourseId}");
    }

    [Fact]
    public void Create_Should_Fail_IfArnContainsWildcard()
    {
        // Act
        var result = ResourceArn.Create("courses", TenantId.ToString(), "course/*");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Identity.Application");
    }

    [Fact]
    public void ResourcePattern_Should_MatchCorrectArn()
    {
        // Arrange
        var arn = ResourceArn.Create("courses", TenantId.ToString(), $"course/{CourseId}").Value;
        var pattern = ResourcePattern.Create($"az:courses:{TenantId}:course/*").Value;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeTrue();
    }

    [Fact]
    public void ResourcePattern_Should_Match_AzStar()
    {
        // Arrange
        var arn = ResourceArn.Create("courses", TenantId.ToString(), $"course/{CourseId}").Value;
        var pattern = ResourcePattern.All;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeTrue();
    }

    [Fact]
    public void ResourcePattern_Should_Match_ServiceWildcard()
    {
        // Arrange
        var arn = ResourceArn.Create("courses", TenantId.ToString(), $"course/{CourseId}").Value;
        var pattern = ResourcePattern.Create($"az:*:*").Value;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeTrue();
    }

    [Fact]
    public void ResourcePattern_Should_Match_GlobalTenant()
    {
        // Arrange
        var arn = ResourceArn.ForUser(Guid.NewGuid()); // global tenant
        var pattern = ResourcePattern.Create("az:identity:global:user/*").Value;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeTrue();
    }

    [Fact]
    public void ResourcePattern_Should_Match_Placeholders()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.Create("courses", TenantId.ToString(), $"course/{courseId}").Value;
        var pattern = ResourcePattern.Create($"az:courses:{TenantId}:course/{{courseId}}").Value;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeTrue();
    }

    [Fact]
    public void ResourcePattern_Should_NotMatch_IfPathSegmentDifferent_WithPlaceholders()
    {
        // Arrange
        var arn = ResourceArn.Create("courses", TenantId.ToString(), $"subject/{Guid.NewGuid()}").Value;
        var pattern = ResourcePattern.Create($"az:courses:{TenantId}:course/{{courseId}}").Value;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeFalse();
    }

    [Fact]
    public void ResourcePattern_Should_NotMatchDifferentTenant()
    {
        // Arrange
        var arn = ResourceArn.Create("courses", Guid.NewGuid().ToString(), $"course/{CourseId}").Value;
        var pattern = ResourcePattern.Create($"az:courses:{TenantId}:course/*").Value;

        // Act & Assert
        pattern.IsMatch(arn).Should().BeFalse();
    }
}
