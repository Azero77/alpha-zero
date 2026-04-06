using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using FluentAssertions;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollements;

public class ProgressTests
{
    [Fact]
    public void CompletionPercentage_Should_ReturnCorrectValue()
    {
        // Arrange
        var progress = Progress.Create(4); // 4 items

        // Act
        var result1 = progress.MarkAsComplete(0);
        var result2 = result1.Value.MarkAsComplete(1);
        progress = result2.Value;

        // Assert
        progress.CompletionPercentage.Should().Be(50.0);
    }

    [Fact]
    public void IsAllComplete_Should_BeTrue_WhenAllBitsAreSet()
    {
        // Arrange
        var progress = Progress.Create(2);

        // Act
        var result1 = progress.MarkAsComplete(0);
        var result2 = result1.Value.MarkAsComplete(1);
        progress = result2.Value;

        // Assert
        progress.IsAllComplete.Should().BeTrue();
    }

    [Fact]
    public void MarkAsComplete_Should_Fail_WhenIndexIsOutOfRange()
    {
        // Arrange
        var progress = Progress.Create(1);

        // Act
        var result = progress.MarkAsComplete(5);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Progress.InvalidIndex");
    }

    [Fact]
    public void IsComplete_Should_ReturnTrue_OnlyForCompletedItems()
    {
        // Arrange
        var progress = Progress.Create(3);

        // Act
        var result = progress.MarkAsComplete(1);
        progress = result.Value;

        // Assert
        progress.IsComplete(0).Should().BeFalse();
        progress.IsComplete(1).Should().BeTrue();
        progress.IsComplete(2).Should().BeFalse();
    }
}