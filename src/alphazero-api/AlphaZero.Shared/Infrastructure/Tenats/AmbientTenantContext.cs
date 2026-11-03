namespace AlphaZero.Shared.Infrastructure.Tenats;

/// <summary>
/// Tenant identity for flows that have no HTTP context (message consumers, background work).
/// Backed by <see cref="AsyncLocal{T}"/> so it survives the child scopes that
/// <c>AppModule.Send</c> creates per dispatch.
/// </summary>
public interface IAmbientTenantContext
{
    Guid? TenantId { get; }

    /// <summary>
    /// Sets the ambient tenant until the returned handle is disposed, restoring any previous value.
    /// </summary>
    IDisposable Push(Guid? tenantId);
}

public sealed class AmbientTenantContext : IAmbientTenantContext
{
    private static readonly AsyncLocal<TenantHolder?> Current = new();

    public Guid? TenantId
    {
        get
        {
            var tenantId = Current.Value?.TenantId;
            return tenantId is null || tenantId == Guid.Empty ? null : tenantId;
        }
    }

    public IDisposable Push(Guid? tenantId) => new Restore(Current.Value, tenantId);

    private sealed record TenantHolder(Guid? TenantId);

    private sealed class Restore : IDisposable
    {
        private readonly TenantHolder? _previous;
        private int _disposed;

        public Restore(TenantHolder? previous, Guid? tenantId)
        {
            _previous = previous;
            Current.Value = new TenantHolder(tenantId);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Current.Value = _previous;
        }
    }
}
