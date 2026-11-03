using System.Collections.Immutable;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Request-scope dual eviction queue (keys + fan-out tag). The lanes are instance state on
/// the scoped object, which guarantees the guardrail the pipeline depends on: decorators queue inside a
/// command handler (necessarily a <em>descendant</em> async context of the MediatR behavior that
/// later saves), and the EF interceptors flush from the behavior that called
/// <c>SaveChangesAsync</c>. AsyncLocal writes made in a descendant do NOT flow back up to the
/// caller, so flow-scoped lanes would always be empty at flush time under the module pipeline;
/// shared instance state does, because decorators, the unit of work, the DbContext and both
/// interceptors resolve from the SAME lifetime scope (everything is registered scoped for exactly
/// this reason). One scope ⇒ one queue; a disposed scope drops its queue with it.
/// <para>
/// <b>Synchronization Protocol:</b>
/// State is held in a single <c>volatile Lanes? _lanes</c> reference pointing to an immutable
/// <see cref="Lanes"/> record containing <see cref="ImmutableHashSet{CacheKey}"/> and
/// <see cref="ImmutableHashSet{CacheTag}"/>.
/// <list type="bullet">
/// <item><description>
/// <b>Atomicity &amp; Snapshot:</b> <see cref="FlushAsync"/> executes a single
/// <see cref="Interlocked.Exchange{T}(ref T, T)"/> replacing <c>_lanes</c> with <see cref="Lanes.Empty"/>.
/// This atomically captures the point-in-time snapshot and clears both lanes in one operation.
/// </description></item>
/// <item><description>
/// <b>No Key Loss:</b> Producers (<see cref="Evict(CacheKey)"/> and <see cref="EvictTag(CacheTag)"/>)
/// read <c>_lanes</c>, create a new immutable <see cref="Lanes"/> instance with the added element, and
/// install it via an <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/> retry loop.
/// A producer racing a flush either succeeds before the exchange (coalescing into the current flush snapshot)
/// or fails CAS / observes <see cref="Lanes.Empty"/> after the exchange (carrying over to the next flush snapshot).
/// Thus, no key or tag is ever lost across any pattern of concurrent saves.
/// </description></item>
/// <item><description>
/// <b>Deduplication:</b> Deduplication is structurally guaranteed by <see cref="ImmutableHashSet{T}"/> semantics;
/// adding an existing key or tag produces the identical set reference.
/// </description></item>
/// <item><description>
/// <b>No Await under Synchronization:</b> Cache I/O (<c>RemoveAsync</c>, <c>RemoveByTagAsync</c>) is awaited
/// outside any synchronization region on the local snapshot reference.
/// </description></item>
/// <item><description>
/// <b>Discard:</b> Atomically resets the active queue back to <see cref="Lanes.Empty"/> without performing any cache operations.
/// </description></item>
/// </list>
/// </para>
/// </summary>
public sealed class ScopedEvictionProvider : IEvictionProvider
{
    private volatile Lanes? _lanes;

    private readonly ICachingService _cache;
    private readonly ILogger<ScopedEvictionProvider> _logger;

    public ScopedEvictionProvider(ICachingService cache, ILogger<ScopedEvictionProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsActive => _lanes is not null;

    public bool HasPendingKeys => _lanes is { Keys.IsEmpty: false };

    public void Begin()
    {
        Interlocked.CompareExchange(ref _lanes, Lanes.Empty, null);
    }

    public void Evict(CacheKey key)
    {
        SpinWait spin = default;
        while (true)
        {
            var current = _lanes;
            var next = new Lanes(
                (current?.Keys ?? ImmutableHashSet<CacheKey>.Empty).Add(key),
                current?.Tags ?? ImmutableHashSet<CacheTag>.Empty);

            if (Interlocked.CompareExchange(ref _lanes, next, current) == current)
                return;

            spin.SpinOnce();
        }
    }

    public void Evict(IEnumerable<CacheKey> keys)
    {
        foreach (var key in keys)
            Evict(key);
    }

    public void EvictTag(CacheTag tag)
    {
        SpinWait spin = default;
        while (true)
        {
            var current = _lanes;
            var next = new Lanes(
                current?.Keys ?? ImmutableHashSet<CacheKey>.Empty,
                (current?.Tags ?? ImmutableHashSet<CacheTag>.Empty).Add(tag));

            if (Interlocked.CompareExchange(ref _lanes, next, current) == current)
                return;

            spin.SpinOnce();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var snapshot = Interlocked.Exchange(ref _lanes, Lanes.Empty);

        if (snapshot is null || (snapshot.Keys.IsEmpty && snapshot.Tags.IsEmpty))
            return;

        foreach (var key in snapshot.Keys)
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

        foreach (var tag in snapshot.Tags)
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
        SpinWait spin = default;
        while (true)
        {
            var current = _lanes;
            if (current is null)
                return;

            if (Interlocked.CompareExchange(ref _lanes, Lanes.Empty, current) == current)
                return;

            spin.SpinOnce();
        }
    }

    private sealed record Lanes(ImmutableHashSet<CacheKey> Keys, ImmutableHashSet<CacheTag> Tags)
    {
        public static readonly Lanes Empty = new(
            ImmutableHashSet<CacheKey>.Empty,
            ImmutableHashSet<CacheTag>.Empty);
    }
}
