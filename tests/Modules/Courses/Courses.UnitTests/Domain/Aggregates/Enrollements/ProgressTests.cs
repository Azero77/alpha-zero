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

    [Fact]
    public void CompletionPercentage_Should_CalculateAgainstActiveItems_WhenItemsWereDeleted()
    {
        // Arrange: 10 allocated bit indexes, but only 8 active items remaining (2 soft-deleted)
        var progress = Progress.Create(10, activeItems: 8);

        // Act: Complete 4 active items
        progress = progress.MarkAsComplete(0).Value;
        progress = progress.MarkAsComplete(1).Value;
        progress = progress.MarkAsComplete(2).Value;
        progress = progress.MarkAsComplete(3).Value;

        // Assert: 4 / 8 = 50% (NOT 4 / 10 = 40%)
        progress.CompletionPercentage.Should().Be(50.0);
    }

    [Fact]
    public void CompletionPercentage_Should_Reach100Percent_WhenAllActiveItemsCompleted()
    {
        // Arrange: 5 total items, only 2 active items
        var progress = Progress.Create(5, activeItems: 2);

        // Act: Complete the 2 active items
        progress = progress.MarkAsComplete(0).Value;
        progress = progress.MarkAsComplete(1).Value;

        // Assert
        progress.CompletionPercentage.Should().Be(100.0);
        progress.IsAllComplete.Should().BeTrue();
    }

    [Fact]
    public void CompletionPercentage_Should_ReturnZero_WhenActiveItemsIsZero()
    {
        // Arrange: Course with no active items
        var progress = Progress.Create(5, activeItems: 0);

        // Assert
        progress.CompletionPercentage.Should().Be(0);
        progress.IsAllComplete.Should().BeTrue();
    }
}