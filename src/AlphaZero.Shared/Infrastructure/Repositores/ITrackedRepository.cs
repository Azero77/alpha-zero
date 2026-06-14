using AlphaZero.Shared.Domain;

namespace AlphaZero.Shared.Infrastructure.Repositores;

public enum TrackedEntryState
{
    Added,
    Modified,
    Removed,
    Unchanged
}

/// <summary>
/// Represents a domain entity being tracked by a DataModel repository.
/// Mirrors EF's EntityEntry concept but for domain models.
/// </summary>
public class TrackedEntry
{
    public Entity DomainEntity { get; }
    public TrackedEntryState State { get; internal set; }

    public TrackedEntry(Entity domainEntity, TrackedEntryState state)
    {
        DomainEntity = domainEntity;
        State = state;
    }
}

/// <summary>
/// Marker interface for repositories that do their own change tracking
/// (as opposed to relying on EF's ChangeTracker).
/// The UnitOfWork discovers all ITrackedRepository instances in the scope
/// to flush changes and harvest domain events.
/// </summary>
public interface ITrackedRepository
{
    IReadOnlyCollection<TrackedEntry> GetTrackedEntries();

    /// <summary>
    /// Flush all tracked changes to the underlying DbContext.
    /// Called by UnitOfWork before DbContext.SaveChangesAsync().
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
