using Microsoft.Extensions.Logging;

namespace AlphaZero.Shared.Infrastructure.Caching;

public sealed class ScopedEvictionProvider : IEvictionProvider
{
    private static readonly AsyncLocal<HashSet<CacheTag>?> Pending = new();

    private readonly ICachingService _cache;
    private readonly ILogger<ScopedEvictionProvider> _logger;

    public ScopedEvictionProvider(ICachingService cache, ILogger<ScopedEvictionProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void Begin() => Pending.Value ??= new HashSet<CacheTag>();

    public void Evict(CacheTag tag)
    {
        var pending = Pending.Value;
        if (pending is null)
        {
            // Should only happen if a save runs outside the behavior; keep the tag anyway.
            pending = new HashSet<CacheTag>();
            Pending.Value = pending;
        }

        pending.Add(tag);
    }

    public void Evict(IEnumerable<CacheTag> tags)
    {
        foreach (var tag in tags)
            Evict(tag);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (Pending.Value is not { Count: > 0 } pending)
            return;

        // Snapshot and clear first: a failed flush must not be retried by an enclosing scope.
        // Clear in place — assigning Pending.Value inside an async method would not be visible
        // to the caller's execution context, so the next dispatch would re-evict stale tags.
        var tags = pending.ToArray();
        pending.Clear();

        foreach (var tag in tags)
        {
            try
            {
                await _cache.RemoveByTagAsync(tag, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The write is already committed; eviction failure may only delay freshness.
                _logger.LogWarning(ex,
                    "Cache eviction failed for tag {Tag}; entries expire within their TTL instead.",
                    tag.Value);
            }
        }
    }

    public void Discard() => Pending.Value?.Clear();
}
