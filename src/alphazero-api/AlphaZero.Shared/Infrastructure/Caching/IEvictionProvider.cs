namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Flow-local eviction queue with two lanes: exact cache keys (the primary eviction currency —
/// every enumerable surface evicts by key) and tags (fan-out only, for families no aggregate
/// argument can enumerate). Queuing happens before the write commits; EF Core interceptors in
/// <c>AlphaZero.Shared.Infrastructure.UnitOfWork</c> flush at DB commit points.
/// The queue lives in <see cref="AsyncLocal{T}"/> because module dispatches create a new child
/// container scope per <c>Send</c>.
/// </summary>
public interface IEvictionProvider
{
    /// <summary>True when a flow-local queue exists, i.e. a flow (or an explicit eviction call) started.</summary>
    bool IsActive { get; }

    /// <summary>True when at least one key is queued for the current flow.</summary>
    bool HasPendingKeys { get; }

    /// <summary>Initializes the flow-local queue; called at the start of a flow.</summary>
    void Begin();

    /// <summary>Queues one exact key for removal at the next flush point.</summary>
    void Evict(CacheKey key);

    /// <summary>Queues a set of exact keys for removal at the next flush point.</summary>
    void Evict(IEnumerable<CacheKey> keys);

    /// <summary>Queues a tag for fan-out removal (the only non-enumerable eviction currency).</summary>
    void EvictTag(CacheTag tag);

    /// <summary>
    /// Removes every queued key (<c>RemoveAsync</c>) and tag (<c>RemoveByTagAsync</c>), clearing both
    /// lanes in place before the calls. Never retried: a failed flush may only delay freshness —
    /// entries die by TTL. <see cref="OperationCanceledException"/> propagates; every other exception
    /// is logged and swallowed because the write has already committed.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken);

    /// <summary>Clears both lanes without touching the cache (failed save / rollback).</summary>
    void Discard();
}
