using System.Reflection;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Repositories;

public class CourseQueryBuilderTests
{
    [Fact]
    public void AsNoTracking_Should_SetNoTrackingFlag_AndReturnSelf()
    {
        // Arrange
        var builder = new CourseQueryBuilder(null!);

        // Act
        var result = builder.AsNoTracking();

        // Assert
        result.Should().BeSameAs(builder);

        var field = typeof(CourseQueryBuilder).GetField("_asNoTracking", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var value = (bool)field!.GetValue(builder)!;
        value.Should().BeTrue();
    }
}
