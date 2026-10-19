using System.Collections;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

/// <summary>
/// Student completion state as a bitmask over course bit positions — the Enrollment side of the
/// (Course.ActiveItemsBitmask, Progress.Bitmask) pair. Only the completed bits are stored:
/// TotalItems and ActiveItems were persisted here once and are now derived exclusively from the
/// course's ActiveItemsBitmask (see ProgressCalculation) — never stored per enrollment.
///
/// The mask grows lazily: a new enrollment starts with a zero-length mask (missing bits are
/// incomplete, never completed) and extends by one bit each time an item is completed.
/// </summary>
public record Progress
{
    public BitArray Bitmask { get; private set; }

    private Progress() { } // For EF Core

    private Progress(BitArray bitmask)
    {
        Bitmask = bitmask;
    }

    /// <summary>A brand-new enrollment: zero completed bits (zero-length mask).</summary>
    public static Progress Create() => new(new BitArray(0, false));

    /// <summary>
    /// Mark a bit complete for the enrollment. Activity validation is intentionally NOT a
    /// progress concern: <c>Progress</c> is just a varbit of completed positions — whether a
    /// bit maps to an active curriculum item is checked by <c>Enrollment.CompleteItem</c>
    /// against the course's ActiveItemsBitmask.
    /// </summary>
    public ErrorOr<Progress> MarkAsComplete(int bitIndex)
    {
        // Only bit positions that map to an active curriculum item on the course can be completed.
        if (bitIndex < 0)
            return Error.Validation("Progress.InvalidIndex", $"Bit index Cannot be with this value", new Dictionary<string, object>()
            {
                ["Value"] = bitIndex
            });
        // Shorter masks are grown with zero bits (missing = incomplete); existing bits are copied defensively.
        var newBitmask = new BitArray(Bitmask);
        if (bitIndex >= newBitmask.Length)
            newBitmask.Length = bitIndex + 1;

        newBitmask.Set(bitIndex, true);
        return new Progress(newBitmask);
    }

    public bool IsComplete(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= Bitmask.Length) return false;
        return Bitmask[bitIndex];
    }
}
