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
    // Lanes are guarded: Evict may legally be called from any thread inside the scope (e.g. a
    // parallel MediatR publish strategy), while FlushAsync/Discard serialize the snapshot-and-clear
    // against concurrent queuers. The cache I/O happens OUTSIDE the lock — only the lane-snapshot
    // bookkeeping is serialized.
    private readonly object _gate = new();
    private HashSet<CacheKey>? _pendingKeys;
    private HashSet<CacheTag>? _pendingTags;

    private readonly ICachingService _cache;
    private readonly ILogger<ScopedEvictionProvider> _logger;

    public ScopedEvictionProvider(ICachingService cache, ILogger<ScopedEvictionProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _pendingKeys is not null || _pendingTags is not null;
            }
        }
    }

    public bool HasPendingKeys
    {
        get
        {
            lock (_gate)
            {
                return _pendingKeys is { Count: > 0 };
            }
        }
    }

    public void Begin()
    {
        lock (_gate)
        {
            _pendingKeys ??= [];
            _pendingTags ??= [];
        }
    }

    public void Evict(CacheKey key)
    {
        lock (_gate)
        {
            _pendingKeys ??= [];
            _pendingKeys.Add(key);
        }
    }

    public void Evict(IEnumerable<CacheKey> keys)
    {
        foreach (var key in keys)
            Evict(key);
    }

    public void EvictTag(CacheTag tag)
    {
        lock (_gate)
        {
            _pendingTags ??= [];
            _pendingTags.Add(tag);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        CacheKey[] keySnapshot;
        CacheTag[] tagSnapshot;
        lock (_gate)
        {
            // Snapshot and clear both lanes under the lock: a failed flush must not be retried by
            // an enclosing scope, and concurrent queuers that land after the snapshot belong to
            // the NEXT save.
            keySnapshot = _pendingKeys is { Count: > 0 } ? _pendingKeys.ToArray() : [];
            _pendingKeys?.Clear();
            tagSnapshot = _pendingTags is { Count: > 0 } ? _pendingTags.ToArray() : [];
            _pendingTags?.Clear();
        }

        if (keySnapshot.Length == 0 && tagSnapshot.Length == 0)
            return;

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
        lock (_gate)
        {
            _pendingKeys?.Clear();
            _pendingTags?.Clear();
        }
    }
}
