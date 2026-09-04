using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AlphaZero.Shared.Infrastructure.SoftDelete
{
    public class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        private readonly IClock clock;

        public SoftDeleteInterceptor(IClock clock)
        {
            this.clock = clock;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context is null)
            {
                return base.SavingChanges(eventData, result);
            }

            var entries = eventData.Context.ChangeTracker.Entries<ISoftDeletable>()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                entry.State = EntityState.Modified;

                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.OnDeleted)).CurrentValue = clock.Now;

                // Prevent owned entities from being NULLed out during soft delete
                MarkOwnedEntitiesAsUnchanged(entry);
            }

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is null)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            var entries = eventData.Context.ChangeTracker.Entries<ISoftDeletable>()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                entry.State = EntityState.Modified;

                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.OnDeleted)).CurrentValue = clock.Now;

                // Prevent owned entities from being NULLed out during soft delete
                MarkOwnedEntitiesAsUnchanged(entry);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void MarkOwnedEntitiesAsUnchanged(EntityEntry entry)
        {
            foreach (var navigationEntry in entry.Navigations)
            {
                if (navigationEntry is ReferenceEntry referenceEntry &&
                    referenceEntry.Metadata.TargetEntityType.IsOwned() &&
                    referenceEntry.TargetEntry != null &&
                    referenceEntry.TargetEntry.State == EntityState.Deleted)
                {
                    referenceEntry.TargetEntry.State = EntityState.Unchanged;
                    MarkOwnedEntitiesAsUnchanged(referenceEntry.TargetEntry);
                }
            }
        }
    }
}
