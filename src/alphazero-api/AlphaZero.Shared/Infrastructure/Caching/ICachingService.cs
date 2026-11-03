namespace AlphaZero.Shared.Infrastructure.Caching;

public interface ICachingService
{
    Task<T> GetOrCreateAsync<T>(
        CacheKey key,
        CachePolicy policy,
        IReadOnlyCollection<CacheTag> tags,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(
        CacheKey key,
        T value,
        CachePolicy policy,
        IReadOnlyCollection<CacheTag> tags,
        CancellationToken cancellationToken = default);

    Task RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken = default);

    /// <summary>Queues one key on the flow-local eviction queue (removed at the commit point).</summary>
    void Evict(CacheKey key);

    /// <summary>Queues a set of keys on the flow-local eviction queue (removed at the commit point).</summary>
    void Evict(IEnumerable<CacheKey> keys);

    void Evict(CacheTag tag);

    void Evict(IEnumerable<CacheTag> tags);
}
