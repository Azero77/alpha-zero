namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Time-to-live for a cached entry. With no distributed cache registered the time-to-live is
/// applied to the local (L1) entry.
/// </summary>
public sealed class CachePolicy
{
    public CachePolicy(TimeSpan timeToLive)
    {
        if (timeToLive <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "Cache time-to-live must be positive.");

        TimeToLive = timeToLive;
    }

    public TimeSpan TimeToLive { get; }
}
