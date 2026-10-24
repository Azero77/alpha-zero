namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Tracks the MediatR nesting depth for a logical operation. It is intentionally
/// <see cref="AsyncLocal{T}"/>-backed: module dispatches run in child container scopes, so a
/// scoped counter would reset and a nested command would flush before the outer commit.
/// </summary>
public sealed class EvictionFlushScope
{
    private static readonly AsyncLocal<int> Depth = new();

    public bool IsOutermost => Depth.Value <= 1;

    public IDisposable Enter()
    {
        Depth.Value++;
        return new Exit();
    }

    private sealed class Exit : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Depth.Value--;
        }
    }
}
