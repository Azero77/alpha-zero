using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// The only place that talks to <see cref="HybridCache"/>. Modules depend on
/// <see cref="ICachingService"/> so the cache implementation (and any future L2) stays
/// behind the shared boundary.
/// </summary>
public sealed class HybridCachingService : ICachingService
{
    private readonly HybridCache _cache;
    private readonly IEvictionProvider _evictionProvider;

    public HybridCachingService(HybridCache cache, IEvictionProvider evictionProvider)
    {
        _cache = cache;
        _evictionProvider = evictionProvider;
    }

    public async Task<T> GetOrCreateAsync<T>(
        CacheKey key,
        CachePolicy policy,
        IReadOnlyCollection<CacheTag> tags,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = policy.TimeToLive,
            LocalCacheExpiration = policy.TimeToLive
        };

        return await _cache.GetOrCreateAsync<Func<CancellationToken, Task<T>>, T>(
            key.Value,
            factory,
            static (cachedFactory, token) => new ValueTask<T>(cachedFactory(token)),
            options,
            tags.Select(static tag => tag.Value),
            cancellationToken);
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
        => await _cache.RemoveAsync(key.Value, cancellationToken);

    public async Task SetAsync<T>(
        CacheKey key,
        T value,
        CachePolicy policy,
        IReadOnlyCollection<CacheTag> tags,
        CancellationToken cancellationToken = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = policy.TimeToLive,
            LocalCacheExpiration = policy.TimeToLive
        };

        await _cache.SetAsync(key.Value, value, options, tags.Select(tag => tag.Value).ToArray(), cancellationToken);
    }

    public async Task RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken = default)
        => await _cache.RemoveByTagAsync(tag.Value, cancellationToken);

    // No need for repositories to know that we are using IEvictionProvider to achieve loose coupling.
    // Keys are the primary eviction currency; tags are fan-out only.
    public void Evict(CacheKey key) => _evictionProvider.Evict(key);

    public void Evict(IEnumerable<CacheKey> keys) => _evictionProvider.Evict(keys);

    public void Evict(CacheTag tag) => _evictionProvider.EvictTag(tag);

    public void Evict(IEnumerable<CacheTag> tags)
    {
        foreach (var tag in tags)
            _evictionProvider.EvictTag(tag);
    }
}
