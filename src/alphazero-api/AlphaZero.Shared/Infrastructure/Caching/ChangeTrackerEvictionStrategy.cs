using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Dispatches every inserted/updated/deleted entity to the module policies that handle its
/// concrete CLR type. Owned entries are skipped: their owner carries the tag.
/// </summary>
public sealed class ChangeTrackerEvictionStrategy : IEvictionStrategy
{
    private readonly IEntityEvictionPolicy[] _policies;
    private readonly IEvictionProvider _provider;

    public ChangeTrackerEvictionStrategy(
        IEnumerable<IEntityEvictionPolicy> policies,
        IEvictionProvider provider)
    {
        _policies = policies.ToArray();
        _provider = provider;
    }

    public async Task CollectAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (_policies.Length == 0)
            return;

        var entries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Metadata.FindOwnership() is null)
            .ToArray();

        foreach (var entry in entries)
        {
            var entityType = entry.Metadata.ClrType;

            foreach (var policy in _policies)
            {
                if (!policy.Handles(entityType))
                    continue;

                var tags = await policy.ResolveTagsAsync(entry, context, cancellationToken);
                if (tags.Count > 0)
                    _provider.Evict(tags);
            }
        }
    }
}
