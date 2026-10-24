using AlphaZero.Shared.Infrastructure.Caching;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

/// <summary>
/// Identity cache keys. These identifiers are intentionally global: a device id and a tenant-user
/// id are globally unique primary keys, and condition definitions are platform-wide rows with no
/// tenant column, so no tenant segment is needed (or available) to keep entries isolated.
/// </summary>
internal static class IdentityCacheKeys
{
    public static CacheKey DevicePublicKey(string deviceId) => CacheKey.Create("device_pubkey", deviceId);

    public static CacheKey UserAssignments(string tenantUserId) => CacheKey.Create("user_assignments", tenantUserId);

    public static CacheKey ConditionDefinition(string name) => CacheKey.Create("conditions:definition", name);
}
