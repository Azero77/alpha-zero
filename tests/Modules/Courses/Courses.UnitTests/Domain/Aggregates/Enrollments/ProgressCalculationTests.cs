using System.Collections;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using FluentAssertions;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

/// <summary>
/// The (course ActiveItemsBitmask × student Progress.Bitmask) derived math:
/// completion percentage, transparent counts and the missing-bits-are-incomplete rule.
/// </summary>
public class ProgressCalculationTests
{
    private static BitArray Mask(int length, params int[] setBits)
    {
        var mask = new BitArray(length, false);
        foreach (var bit in setBits) mask[bit] = true;
        return mask;
    }

    [Fact]
    public void CompletionPercentage_Should_CountOnlyCompletedActiveItems()
    {
        // Course: positions 0..3 active (position 4 deleted). Student completed 0, 1 and 4.
        var student = Mask(5, 0, 1, 4);
        var course = Mask(5, 0, 1, 2, 3);

        ProgressCalculation.CompletedActiveItems(student, course).Should().Be(2);
        ProgressCalculation.ActiveItems(course).Should().Be(4);
        ProgressCalculation.CompletionPercentage(student, course).Should().Be(50.0); // 2 / 4 — the stale bit 4 must not inflate
    }

    [Fact]
    public void CompletionPercentage_Should_TreatMissingBitsAsIncomplete()
    {
        // Enrollment mask shorter than the course mask: positions 2..3 must count as incomplete.
        var student = Mask(2, 1); // position 0 inactive on course + student never completed it
        var course = Mask(4, 1, 2, 3);

        ProgressCalculation.CompletionPercentage(student, course).Should().BeApproximately(33.33, 0.01, because: "1 of 3 active items completed");
        ProgressCalculation.CompletedActiveItems(student, course).Should().Be(1);
    }

    [Fact]
    public void CompletionPercentage_Should_ReturnZero_WhenCourseHasNoActiveItems()
    {
        var student = Mask(4, 0, 1, 2, 3);

        ProgressCalculation.CompletionPercentage(student, new BitArray(0, false)).Should().Be(0);
        ProgressCalculation.IsAllComplete(student, new BitArray(0, false)).Should().BeTrue();
    }

    [Fact]
    public void IsAllComplete_Should_UseTheActiveDenominator()
    {
        var student = Mask(5, 0, 1);
        var course = Mask(5, 0, 1); // only 2 active of 5 allocated

        ProgressCalculation.IsAllComplete(student, course).Should().BeTrue();
        ProgressCalculation.CompletionPercentage(student, course).Should().Be(100.0);
    }

    [Fact]
    public void ActiveItems_Should_UsePackedWordPopulation()
    {
        // Exercises the ulong PopCount path (> 64 bits) — 1 byte, 1 bit and population spread over 8-bit words.
        var course = Mask(100, 3, 70, 71, 99);

        ProgressCalculation.ActiveItems(course).Should().Be(4);
    }

    [Fact]
    public void ActiveItems_Should_BeZero_ForEmptyCourse()
    {
        ProgressCalculation.ActiveItems(new BitArray(0, false)).Should().Be(0);
    }

    [Fact]
    public void CompletionPercentage_Should_NotDecreasePast100()
    {
        // Defensive cap: stale/short masks can never push the ratio above 100.
        var student = Mask(2, 0, 1);
        var course = Mask(3, 0); // 1 active; student completed 0 and 1

        ProgressCalculation.CompletionPercentage(student, course).Should().Be(100.0);
    }
}
