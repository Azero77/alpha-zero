using System.Collections;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;

public record Progress
{
    // The bitmask as a BitArray to represent completed items
    public BitArray Bitmask { get; private set; }
    public int TotalItems { get; private set; }
    public int ActiveItems { get; private set; }

    private Progress() { } // For EF Core or serialization

    private Progress(int totalItems, int? activeItems = null)
    {
        TotalItems = totalItems;
        ActiveItems = activeItems ?? totalItems;
        Bitmask = new BitArray(totalItems, false);
    }

    private Progress(BitArray bitmask, int? activeItems = null)
    {
        Bitmask = bitmask;
        TotalItems = bitmask.Length;
        ActiveItems = activeItems ?? bitmask.Length;
    }

    public static Progress Create(int totalItems, int? activeItems = null)
    {
        return new Progress(totalItems, activeItems);
    }

    // Re-creating from DB storage (BitArray is easy to store as byte[] or bit strings)
    public static Progress FromBitmask(BitArray bitmask, int? activeItems = null)
    {
        return new Progress(bitmask, activeItems);
    }

    public ErrorOr<Progress> MarkAsComplete(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= TotalItems)
        {
            return Error.Validation("Progress.InvalidIndex", $"Bit index {bitIndex} is out of range for this course (0-{TotalItems-1}).");
        }
        var newBitMask = new BitArray(Bitmask);
        newBitMask.Set(bitIndex, true);
        return new Progress(newBitMask, ActiveItems);
    }

    public bool IsComplete(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= TotalItems) return false;
        return Bitmask.Get(bitIndex);
    }

    public double CompletionPercentage
    {
        get
        {
            if (ActiveItems <= 0) return 0;
            int completedCount = 0;
            for (int i = 0; i < Bitmask.Length; i++)
            {
                if (Bitmask[i]) completedCount++;
            }
            return Math.Min(100.0, (double)completedCount / ActiveItems * 100);
        }
    }

    public bool IsAllComplete
    {
        get
        {
            if (ActiveItems <= 0) return true;
            int completedCount = 0;
            for (int i = 0; i < Bitmask.Length; i++)
            {
                if (Bitmask[i]) completedCount++;
            }
            return completedCount >= ActiveItems;
        }
    }
}
