using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Models;

public class ResourceArnTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.NewGuid();

    [Fact]
    public void ToString_Should_ReturnCorrectFormat()
    {
        // Arrange
        var arn = ResourceArn.ForCourse(TenantId, CourseId);

        // Act
        var result = arn.ToString();

        // Assert
        result.Should().Be($"az:courses:{TenantId.ToString().ToLowerInvariant()}:course/{CourseId}");
    }

    [Theory]
    [InlineData("az:courses:{tid}:course/{cid}", true)] // Exact match
    [InlineData("az:courses:{tid}:course/*", true)]    // Parent wildcard
    [InlineData("az:courses:{tid}:*", true)]           // Service wildcard
    [InlineData("az:courses:global:course/{cid}", false)] // Wrong tenant
    [InlineData("az:video:{tid}:course/{cid}", false)]   // Wrong service
    [InlineData("az:courses:{tid}:course/{cid}/section/1", false)] // Sub-resource doesn't match parent exact
    public void IsMatchedBy_Should_HandleWildcardsCorrectly(string pattern, bool expected)
    {
        // Arrange
        var arn = ResourceArn.ForCourse(TenantId, CourseId);
        var formattedPattern = pattern
            .Replace("{tid}", TenantId.ToString())
            .Replace("{cid}", CourseId.ToString());

        // Act
        var result = arn.IsMatchedBy(formattedPattern);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ForLesson_Should_IncludeCourseAndSectionContext()
    {
        // Arrange
        var lessonId = Guid.NewGuid();

        // Act
        var arn = ResourceArn.ForLesson(TenantId, CourseId, SectionId, lessonId);

        // Assert
        arn.ToString().Should().Contain($"course/{CourseId}");
        arn.ToString().Should().Contain($"section/{SectionId}");
        arn.ToString().Should().Contain($"lesson/{lessonId}");
    }

    [Fact]
    public void GlobalResources_Should_UseGlobalTenantString()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var arn = ResourceArn.ForUser(userId);

        // Assert
        arn.TenantIdString.Should().Be("global");
        arn.ToString().Should().Be($"az:identity:global:user/{userId}");
    }
}
