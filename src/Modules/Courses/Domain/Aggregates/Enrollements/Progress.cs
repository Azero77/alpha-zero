using System.Collections;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;

public record Progress
{
    // The bitmask as a BitArray to represent completed items
    public BitArray Bitmask { get; private set; }
    public int TotalItems { get; private set; }

    private Progress() { } // For EF Core or serialization

    private Progress(int totalItems)
    {
        TotalItems = totalItems;
        Bitmask = new BitArray(totalItems, false);
    }

    private Progress(BitArray bitmask)
    {
        Bitmask = bitmask;
        TotalItems = bitmask.Length;
    }

    public static Progress Create(int totalItems)
    {
        return new Progress(totalItems);
    }

    // Re-creating from DB storage (BitArray is easy to store as byte[] or bit strings)
    public static Progress FromBitmask(BitArray bitmask)
    {
        return new Progress(bitmask);
    }

    public ErrorOr<Progress> MarkAsComplete(int bitIndex)
    {
        if (bitIndex < 0 || bitIndex >= TotalItems)
        {
            return Error.Validation("Progress.InvalidIndex", $"Bit index {bitIndex} is out of range for this course (0-{TotalItems-1}).");
        }
        var newBitMask = new BitArray(Bitmask);
        newBitMask.Set(bitIndex, true);
        return new Progress(newBitMask);
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
            if (TotalItems == 0) return 0;
            int completedCount = 0;
            for (int i = 0; i < Bitmask.Length; i++)
            {
                if (Bitmask[i]) completedCount++;
            }
            return (double)completedCount / TotalItems * 100;
        }
    }

    public bool IsAllComplete
    {
        get
        {
            for (int i = 0; i < Bitmask.Length; i++)
            {
                if (!Bitmask[i]) return false;
            }
            return true;
        }
    }
}
