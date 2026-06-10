using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Microsoft.Extensions.Caching.Hybrid;


public class PublicKeyProvider(AppDbContext context) : IPublicKeyProvider
{
    public async Task<string?> GetPublicKeyAsync(string deviceId,CancellationToken token = default)
    {
        if (!Guid.TryParse(deviceId, out Guid result))
            return null;
        
        var device = await context.UserDevices.FindAsync(result, token);
        return device?.PublicKey;
    }
}


public class CachePublicKeyProvider(HybridCache cache, PublicKeyProvider decorated) : IPublicKeyProvider
{
    public async Task<string?> GetPublicKeyAsync(string deviceId, CancellationToken token = default)
    {
        return await cache.GetOrCreateAsync<string?>(
            $"device_pubkey:{deviceId}",
            async token => await decorated.GetPublicKeyAsync(deviceId),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(24) }
        );
    }
}