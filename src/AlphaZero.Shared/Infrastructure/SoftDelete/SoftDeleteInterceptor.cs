using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Reflection;

namespace AlphaZero.Shared.Infrastructure.SoftDelete
{
    public  class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        private readonly IClock clock;

        public SoftDeleteInterceptor(IClock clock)
        {
            this.clock = clock;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, 
            InterceptionResult<int> result, 
            CancellationToken cancellationToken = default)
        {

            if (eventData.Context is null)
            {
                return base.SavingChangesAsync(
                    eventData, result, cancellationToken);
            }

            IEnumerable<EntityEntry<ISoftDeletable>> entries =
                eventData
                    .Context
                    .ChangeTracker
                    .Entries<ISoftDeletable>()
                    .Where(e => e.State == EntityState.Deleted);

            foreach (EntityEntry<ISoftDeletable> softDeletable in entries)
            {
                softDeletable.State = EntityState.Modified;

                Type type = softDeletable.Entity.GetType();
                type
                    .GetProperty("IsDeleted")!
                    .SetValue(softDeletable.Entity, true);

                type.GetProperty("OnDeleted")!
                    .SetValue(softDeletable.Entity,clock.Now);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
