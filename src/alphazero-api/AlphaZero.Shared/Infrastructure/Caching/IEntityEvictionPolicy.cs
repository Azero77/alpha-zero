using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Lets a module decide which cache tags a persisted entity invalidates. Implementations are
/// registered by the owning module; the shared change-tracker strategy only dispatches.
/// </summary>
public interface IEntityEvictionPolicy
{
    bool Handles(Type entityType);

    Task<IReadOnlyCollection<CacheTag>> ResolveTagsAsync(
        EntityEntry entry,
        DbContext context,
        CancellationToken cancellationToken);
}
