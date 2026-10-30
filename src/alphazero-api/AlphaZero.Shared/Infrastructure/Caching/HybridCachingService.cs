using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// The only place that talks to <see cref="HybridCache"/>. Modules depend on
/// <see cref="ICachingService"/> so the cache implementation (and any future L2) stays
/// behind the shared boundary.
/// </summary>
public sealed class HybridCachingService : ICachingService
{
    private readonly HybridCache _cache;

    // The Evict pass-throughs resolve the request-scoped eviction queue LAZILY: the provider
    // depends back on ICachingService to flush, so a constructor link service → provider would be
    // a strict constructor cycle (Autofac rejects it the moment any scoped resolution touches the
    // cache — every AppDbContext creation does, via the eviction interceptors). Both services
    // share the lifetime scope, so a resolve-at-call yields the same queue instance.
    private readonly IServiceProvider _services;

    public HybridCachingService(HybridCache cache, IServiceProvider services)
    {
        _cache = cache;
        _services = services;
    }

    private IEvictionProvider Eviction => _services.GetRequiredService<IEvictionProvider>();

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

    public void Evict(CacheKey key) => Eviction.Evict(key);

    public void Evict(IEnumerable<CacheKey> keys) => Eviction.Evict(keys);

    public void Evict(CacheTag tag) => Eviction.EvictTag(tag);

    public void Evict(IEnumerable<CacheTag> tags)
    {
        foreach (var tag in tags)
            Eviction.EvictTag(tag);
    }
}
