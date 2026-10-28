using Microsoft.Extensions.Logging;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Flow-local dual eviction queue. The queues are <c>static readonly AsyncLocal</c> state, so this
/// instance itself holds nothing per-flow and may be registered as a singleton (or scoped) without
/// changing semantics: every flow — including EF interceptor events — sees its own lanes because
/// <see cref="AsyncLocal{T}"/> flows across async boundaries.
/// </summary>
public sealed class ScopedEvictionProvider : IEvictionProvider
{
    private static readonly AsyncLocal<HashSet<CacheKey>?> PendingKeys = new();
    private static readonly AsyncLocal<HashSet<CacheTag>?> PendingTags = new();

    private readonly ICachingService _cache;
    private readonly ILogger<ScopedEvictionProvider> _logger;

    public ScopedEvictionProvider(ICachingService cache, ILogger<ScopedEvictionProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsActive => PendingKeys.Value is not null || PendingTags.Value is not null;

    public bool HasPendingKeys => PendingKeys.Value is { Count: > 0 };

    public void Begin()
    {
        PendingKeys.Value ??= [];
        PendingTags.Value ??= [];
    }

    public void Evict(CacheKey key)
    {
        var pending = PendingKeys.Value;
        if (pending is null)
        {
            // Should only happen if a save runs outside a flow; keep the key anyway.
            pending = [];
            PendingKeys.Value = pending;
        }

        pending.Add(key);
    }

    public void Evict(IEnumerable<CacheKey> keys)
    {
        foreach (var key in keys)
            Evict(key);
    }

    public void EvictTag(CacheTag tag)
    {
        var pending = PendingTags.Value;
        if (pending is null)
        {
            // Should only happen if a save runs outside a flow; keep the tag anyway.
            pending = [];
            PendingTags.Value = pending;
        }

        pending.Add(tag);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var keyLane = PendingKeys.Value;
        var tagLane = PendingTags.Value;

        if ((keyLane is null || keyLane.Count == 0) && (tagLane is null || tagLane.Count == 0))
            return;

        // Snapshot and clear both lanes first: a failed flush must not be retried by an enclosing
        // scope. Clear IN PLACE — assigning PendingX.Value after an await would not be visible to
        // the caller's execution context, so the next flow would re-evict stale entries.
        var keySnapshot = keyLane is { Count: > 0 } ? keyLane.ToArray() : [];
        keyLane?.Clear();
        var tagSnapshot = tagLane is { Count: > 0 } ? tagLane.ToArray() : [];
        tagLane?.Clear();

        foreach (var key in keySnapshot)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The write is already committed; eviction failure may only delay freshness.
                _logger.LogWarning(ex,
                    "Cache eviction failed for key {Key}; the entry expires within its TTL instead.",
                    key.Value);
            }
        }

        foreach (var tag in tagSnapshot)
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

    public void Discard()
    {
        PendingKeys.Value?.Clear();
        PendingTags.Value?.Clear();
    }
}
