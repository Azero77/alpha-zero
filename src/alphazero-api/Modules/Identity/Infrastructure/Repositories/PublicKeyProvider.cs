using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Modules.Identity.Infrastructure.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Caching;


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


public class CachePublicKeyProvider(
    ICachingService cache,
    PublicKeyProvider decorated) : IPublicKeyProvider
{
    private static readonly CachePolicy Policy = new(TimeSpan.FromHours(24));

    public async Task<string?> GetPublicKeyAsync(string deviceId, CancellationToken token = default)
    {
        if (!Guid.TryParse(deviceId, out var parsedDeviceId))
            return null;

        return await cache.GetOrCreateAsync<string?>(
            IdentityCacheKeys.DevicePublicKey(parsedDeviceId.ToString("N")),
            Policy,
            [],
            cachedToken => decorated.GetPublicKeyAsync(parsedDeviceId.ToString(), cachedToken),
            token);
    }

    public async Task SetNewDevicePublicKey(string tenantUserId, string deviceId, string publicKey, CancellationToken token = default)
    {
        // Reads normalise the device id to "N"; write the same shape or the entry is never hit.
        if (Guid.TryParse(deviceId, out var parsedDeviceId))
        {
            await cache.SetAsync(
                IdentityCacheKeys.DevicePublicKey(parsedDeviceId.ToString("N")),
                publicKey,
                Policy,
                [],
                token);
        }

        // MainDeviceId might have changed, so the user's assignment view must be re-read.
        await cache.RemoveAsync(IdentityCacheKeys.UserAssignments(tenantUserId), token);
    }
}
