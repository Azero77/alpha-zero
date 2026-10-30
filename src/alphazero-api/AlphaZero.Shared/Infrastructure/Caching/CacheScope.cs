namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// A tenant bound to cache operations. When no tenant is available the scope is disabled
/// and every call must fall through to the database: entries are never written or read
/// under a placeholder tenant.
/// </summary>
public readonly record struct CacheScope
{
    public Guid? TenantId { get; }

    private CacheScope(Guid? tenantId) => TenantId = tenantId == Guid.Empty ? null : tenantId;

    public static CacheScope ForTenant(Guid? tenantId) => new(tenantId);

    public bool IsEnabled => TenantId.HasValue;

    public CacheKey Key(string family, params string[] parts) => CacheKey.Create(family, [Prefix(), .. parts]);

    public CacheTag Tag(string family, params string[] parts) => CacheTag.Create(family, [Prefix(), .. parts]);

    private string Prefix()
    {
        if (TenantId is not { } tenantId)
            throw new InvalidOperationException(
                "Cache keys and tags require a tenant. Check CacheScope.IsEnabled and bypass the cache when no tenant is available.");

        return tenantId.ToString("N");
    }
}
