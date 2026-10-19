using System.Buffers.Binary;
using System.Collections;
using System.Numerics;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

/// <summary>
/// Pure calculations over the pair (student completion bitmask, course active-items bitmask).
///
/// The Course owns curriculum activity state (Course.ActiveItemsBitmask) and the Enrollment
/// owns student completion state (Progress.Bitmask). Neither side keeps a persisted count of
/// the other — every count/percentage below is derived from the two masks at calculation time:
///
///   ActiveItemsBitmask[i] == true  → the curriculum position i is an active item
///   ActiveItemsBitmask[i] == false → the position is deleted or has never been allocated
///
/// The enrollment mask may be shorter than the course mask (new enrollments start empty and
/// grow lazily): any missing bit is treated as NOT completed, never as completed.
/// </summary>
public static class ProgressCalculation
{
    /// <summary>Population count of the active-items mask. Positions that were allocated but are no longer active do not count.</summary>
    public static int ActiveItems(BitArray activeItemsBitmask)
        => PopCount(activeItemsBitmask);

    /// <summary>
    /// Number of completed items that are still active:
    /// PopCount(completedBitmask AND activeItemsBitmask), short masks treated as incomplete past their length.
    /// </summary>
    public static int CompletedActiveItems(BitArray completedBitmask, BitArray activeItemsBitmask)
    {
        var sharedLength = Math.Min(completedBitmask.Length, activeItemsBitmask.Length);
        var intersection = new BitArray(completedBitmask.Length, false);
        for (var i = 0; i < sharedLength; i++)
        {
            if (completedBitmask[i] && activeItemsBitmask[i])
                intersection[i] = true;
        }
        return PopCount(intersection);
    }

    /// <summary>
    /// Completion percentage — completed ACTIVE items / active items:
    ///   0 when the course has no active items (no division by zero),
    ///   capped at 100 (a stale mask can never exceed the denominator).
    /// </summary>
    public static double CompletionPercentage(BitArray completedBitmask, BitArray activeItemsBitmask)
    {
        var activeItems = ActiveItems(activeItemsBitmask);
        if (activeItems <= 0) return 0;

        return Math.Min(100.0, (double)CompletedActiveItems(completedBitmask, activeItemsBitmask) / activeItems * 100.0);
    }

    public static bool IsAllComplete(BitArray completedBitmask, BitArray activeItemsBitmask)
    {
        var activeItems = ActiveItems(activeItemsBitmask);
        if (activeItems <= 0) return true;

        return CompletedActiveItems(completedBitmask, activeItemsBitmask) >= activeItems;
    }

    /// <summary>
    /// Population count via System.Numerics.PopCount over ulong words (no per-bit iteration in C#).
    /// </summary>
    private static int PopCount(BitArray bits)
    {
        if (bits.Length == 0) return 0;

        // BitArray.CopyTo writes bits packed LSB-first into bytes when the destination is byte[].
        var bytes = new byte[(bits.Length + 7) / 8];
        bits.CopyTo(bytes, 0);

        var count = 0;
        var span = bytes.AsSpan();

        for (var offset = 0; offset + sizeof(ulong) <= span.Length; offset += sizeof(ulong))
        {
            count += BitOperations.PopCount(BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, sizeof(ulong))));
        }

        var remainder = span.Length % sizeof(ulong);
        if (remainder > 0)
        {
            Span<byte> tail = stackalloc byte[sizeof(ulong)];
            span[^remainder..].CopyTo(tail); // remaining bytes stay zero-packed
            count += BitOperations.PopCount(BinaryPrimitives.ReadUInt64LittleEndian(tail));
        }

        return count;
    }
}
