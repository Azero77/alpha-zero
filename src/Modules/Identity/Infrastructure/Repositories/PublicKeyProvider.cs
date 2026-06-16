using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Autofac.Core;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;


public class PublicKeyProvider(AppDbContext context) : IPublicKeyProvider
{
    public async Task<string?> GetPublicKeyAsync(string deviceId,CancellationToken token = default)
    {
        if (!Guid.TryParse(deviceId, out Guid result))
            return null;
        
        var device = await context.UserDevices.FindAsync(result, token);
        return device?.PublicKey;
    }
    public Task SetNewDevicePublicKey(string tenantUserId, string deviceId, string publicKey, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
}


public class CachePublicKeyProvider(HybridCache cache, 
    PublicKeyProvider decorated,
    IMemoryCache   assignmentCache) : IPublicKeyProvider
{
    public async Task<string?> GetPublicKeyAsync(string deviceId, CancellationToken token = default)
    {
        return await cache.GetOrCreateAsync<string?>(
            $"device_pubkey:{deviceId}",
            async token => await decorated.GetPublicKeyAsync(deviceId),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(24) }
        );
    }

    public async Task SetNewDevicePublicKey(string tenantUserId, string deviceId, string publicKey, CancellationToken token = default)
    {
        await cache.SetAsync(
           $"device_pubkey:{deviceId}",
           publicKey,
           new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(24) },
           cancellationToken: token
       );


        // Also invalidate the user's assignments cache because MainDeviceId might have changed
        assignmentCache.Remove($"user_assignments:{tenantUserId}");

    }
}