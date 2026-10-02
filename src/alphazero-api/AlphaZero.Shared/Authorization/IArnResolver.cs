namespace AlphaZero.Shared.Authorization;

/// <summary>
/// Cross-module interface for verifying asset existence within a container.
/// Registered by the owning module, consumed by external modules for permission checks.
/// </summary>
public interface IArnResolver
{
    Task<bool> IsResourceInContainerAsync(Guid containerId, Guid resourceId, CancellationToken ct = default);
}
