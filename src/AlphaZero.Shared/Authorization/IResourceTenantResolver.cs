namespace AlphaZero.Shared.Authorization;

public interface IResourceTenantResolver
{
    ResourceType ResourceType { get; }
    Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct);
}
