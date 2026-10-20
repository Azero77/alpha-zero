using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using FluentAssertions;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

public class ProgressTests
{
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
        // Arrange: enrollment mask still empty; completing bit 2 extends it lazily.
        var progress = Progress.Create();

        var result = progress.MarkAsComplete(2);

        // Assert: fresh instance, lazily extended to cover the bit set, original untouched
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeSameAs(progress);
        result.Value.Bitmask.Length.Should().Be(3);
        result.Value.Bitmask[2].Should().BeTrue();
        progress.Bitmask.Length.Should().Be(0); // immutable boundary
    }

    [Fact]
    public void MarkAsComplete_Should_Grow_WhenIndexExceedsCurrentLength()
    {
        // Out-of-range is not an error anymore: the mask grows with zero bits
        // (missing = incomplete) up to the bit being set.
        var progress = Progress.Create().MarkAsComplete(0).Value;

        var result = progress.MarkAsComplete(5);

        result.IsError.Should().BeFalse();
        result.Value.Bitmask.Length.Should().Be(6);
        result.Value.Bitmask[5].Should().BeTrue();
    }

    [Fact]
    public void MarkAsComplete_Should_Fail_WhenIndexIsNegative()
    {
        var progress = Progress.Create();
        var result = progress.MarkAsComplete(-1);
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Progress.InvalidIndex");
    }

    [Fact]
    public void IsComplete_Should_ReturnTrue_OnlyForCompletedItems()
    {
        var progress = Progress.Create().MarkAsComplete(1).Value;

        progress.IsComplete(0).Should().BeFalse();
        progress.IsComplete(1).Should().BeTrue();
        progress.IsComplete(2).Should().BeFalse();
        progress.IsComplete(3).Should().BeFalse(); // beyond allocation → incomplete, never completed
    }

    [Fact]
    public void MarkAsComplete_Should_BeIdempotent_ForTheSameBit()
    {
        var progress = Progress.Create().MarkAsComplete(1).Value;

        var again = progress.MarkAsComplete(1);

        again.IsError.Should().BeFalse();
        again.Value.Bitmask[1].Should().BeTrue();
    }
}
