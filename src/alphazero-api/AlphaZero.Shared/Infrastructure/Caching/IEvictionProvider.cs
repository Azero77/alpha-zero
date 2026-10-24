namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Flow-local queue of tags to evict. Queuing happens before the commit; the outermost MediatR
/// behavior flushes after it. The queue lives in an <see cref="AsyncLocal{T}"/> because module
/// dispatches create a new child container scope per <c>Send</c>.
/// </summary>
public interface IEvictionProvider
{
    /// <summary>Initializes the flow-local queue; called by the outermost behavior.</summary>
    void Begin();

    void Evict(CacheTag tag);

    void Evict(IEnumerable<CacheTag> tags);

    Task FlushAsync(CancellationToken cancellationToken);

    void Discard();
}
