using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

public class ProgressWithActiveItemsTests
{
    [Fact]
    public void WithActiveItems_Should_KeepEarnedBits_AndRecomputePercentage()
    {
        // Arrange: 3 tracked items, student completes bit index 2
        var progress = Progress.Create(3);
        progress = progress.MarkAsComplete(2).Value;
        progress.CompletionPercentage.Should().BeApproximately(33.33, 0.01);

        // Act: two items become inactive (section deleted), 1 active item remains
        var updated = progress.WithActiveItems(1);

        // Assert: bit is untouched, percentage remarshals against the new active count
        updated.IsComplete(2).Should().BeTrue();
        updated.CompletionPercentage.Should().Be(100.0);
        updated.IsAllComplete.Should().BeTrue();
    }

    [Fact]
    public void WithActiveItems_Should_ReturnCopy_NotMutateOriginal()
    {
        // Arrange
        var progress = Progress.Create(4);
        progress = progress.MarkAsComplete(0).Value;

        // Act
        var updated = progress.WithActiveItems(2);

        // Assert
        progress.ActiveItems.Should().Be(4);
        progress.CompletionPercentage.Should().Be(25.0);
        updated.ActiveItems.Should().Be(2);
        updated.CompletionPercentage.Should().Be(50.0);

        // Original completion state survives on the copy
        updated.IsComplete(0).Should().BeTrue();
    }

    [Fact]
    public void WithActiveItems_Should_ClampNegativeCounts()
    {
        // Arrange
        var progress = Progress.Create(2);

        // Act
        var updatedNegative = progress.WithActiveItems(-3);

        // Assert
        updatedNegative.ActiveItems.Should().Be(0);
    }
}
