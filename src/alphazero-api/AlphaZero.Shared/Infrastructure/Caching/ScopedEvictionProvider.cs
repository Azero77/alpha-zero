using Microsoft.Extensions.Logging;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Request-scope dual eviction queue (keys + fan-out tag). The lanes are <c>HashSet</c> state on
/// the instance, which guarantees the guardrail the pipeline depends on: decorators queue inside a
/// command handler (necessarily a <em>descendant</em> async context of the MediatR behavior that
/// later saves), and the EF interceptors flush from the behavior that called
/// <c>SaveChangesAsync</c>. AsyncLocal writes made in a descendant do NOT flow back up to the
/// caller, so flow-scoped lanes would always be empty at flush time under the module pipeline;
/// shared instance state does, because decorators, the unit of work, the DbContext and both
/// interceptors resolve from the SAME lifetime scope (everything is registered scoped for exactly
/// this reason). One scope ⇒ one queue; a disposed scope drops its queue with it.
/// </summary>
public sealed class ScopedEvictionProvider : IEvictionProvider
{
    private HashSet<CacheKey>? _pendingKeys;
    private HashSet<CacheTag>? _pendingTags;

    private readonly ICachingService _cache;
    private readonly ILogger<ScopedEvictionProvider> _logger;

    public ScopedEvictionProvider(ICachingService cache, ILogger<ScopedEvictionProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsActive => _pendingKeys is not null || _pendingTags is not null;

    public bool HasPendingKeys => _pendingKeys is { Count: > 0 };

    public void Begin()
    {
        _pendingKeys ??= [];
        _pendingTags ??= [];
    }

    public void Evict(CacheKey key)
    {
        var pending = _pendingKeys;
        if (pending is null)
        {
            // Should only happen if a save runs outside a flow; keep the key anyway.
            pending = [];
            _pendingKeys = pending;
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
        var pending = _pendingTags;
        if (pending is null)
        {
            // Should only happen if a save runs outside a flow; keep the tag anyway.
            pending = [];
            _pendingTags = pending;
        }

        pending.Add(tag);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var keyLane = _pendingKeys;
        var tagLane = _pendingTags;

        if ((keyLane is null || keyLane.Count == 0) && (tagLane is null || tagLane.Count == 0))
            return;

        // Snapshot and clear both lanes first: a failed flush must not be retried by an enclosing
        // scope.
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
        _pendingKeys?.Clear();
        _pendingTags?.Clear();
    }
}
