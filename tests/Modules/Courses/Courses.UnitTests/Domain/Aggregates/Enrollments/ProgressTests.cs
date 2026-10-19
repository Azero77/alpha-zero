using System.Collections;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using FluentAssertions;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

public class ProgressTests
{
    /// <summary>Builds a course active-items mask of the given length with the given positions active.</summary>
    private static BitArray ActiveMask(int length, params int[] activeBits)
    {
        var mask = new BitArray(length, false);
        foreach (var bit in activeBits) mask[bit] = true;
        return mask;
    }

    [Fact]
    public void Create_Should_StartWithZeroLengthMask()
    {
        // A brand-new enrollment carries no persisted counts and no bits: missing = incomplete.
        var progress = Progress.Create();
        progress.Bitmask.Length.Should().Be(0);
    }

    [Fact]
    public void MarkAsComplete_Should_GrowLazyMask_AndReturnNewInstance()
    {
        // Arrange: course has 3 active positions, enrollment mask still empty
        var progress = Progress.Create();

        var result = progress.MarkAsComplete(2, ActiveMask(3, 0, 1, 2));

        // Assert: fresh instance, lazily extended to cover the bit set, original untouched
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeSameAs(progress);
        result.Value.Bitmask.Length.Should().Be(3);
        result.Value.Bitmask[2].Should().BeTrue();
        progress.Bitmask.Length.Should().Be(0); // immutable boundary
    }

    [Fact]
    public void MarkAsComplete_Should_Fail_WhenIndexIsOutOfRange()
    {
        var progress = Progress.Create();
        var result = progress.MarkAsComplete(5, ActiveMask(3, 0, 1, 2));
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Progress.InvalidIndex");
    }

    [Fact]
    public void MarkAsComplete_Should_Fail_WhenIndexIsNegative()
    {
        var progress = Progress.Create();
        var result = progress.MarkAsComplete(-1, ActiveMask(3, 0, 1, 2));
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Progress.InvalidIndex");
    }

    [Fact]
    public void MarkAsComplete_Should_Fail_WhenItemIsNotActive()
    {
        // Deleted / never-allocated course positions must reject completion writes.
        var progress = Progress.Create();

        var result = progress.MarkAsComplete(1, ActiveMask(3, 0, 2));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Progress.ItemInactive");
    }

    [Fact]
    public void IsComplete_Should_ReturnTrue_OnlyForCompletedItems()
    {
        var progress = Progress.Create().MarkAsComplete(1, ActiveMask(3, 0, 1, 2)).Value;

        progress.IsComplete(0).Should().BeFalse();
        progress.IsComplete(1).Should().BeTrue();
        progress.IsComplete(2).Should().BeFalse();
        progress.IsComplete(3).Should().BeFalse(); // beyond allocation → incomplete, never completed
    }

    [Fact]
    public void MarkAsComplete_Should_BeIdempotent_ForTheSameBit()
    {
        var progress = Progress.Create().MarkAsComplete(1, ActiveMask(3, 0, 1, 2)).Value;

        var again = progress.MarkAsComplete(1, ActiveMask(3, 0, 1, 2));

        again.IsError.Should().BeFalse();
        again.Value.Bitmask[1].Should().BeTrue();
    }
}
