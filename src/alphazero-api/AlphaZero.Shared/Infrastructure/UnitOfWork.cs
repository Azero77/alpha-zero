using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Repositores;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure;

public class UnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    TContext _context;
    IPublisher _publisher;
    IEvictionStrategy _evictionStrategy;
    private readonly List<AggregateRoot> _trackedEntities = new();
    public UnitOfWork(TContext context, IPublisher publisher, IEvictionStrategy evictionStrategy)
    {
        _context = context;
        _publisher = publisher;
        _evictionStrategy = evictionStrategy;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var efDomainEvents = _context.ChangeTracker
                .Entries<AggregateRoot>()
                .SelectMany(e => e.Entity.PopDomainEvents())
                .ToList();

            var trackedDataModelsEvents = _trackedEntities.SelectMany(e => e.PopDomainEvents()).ToList();
            var domainEvents = trackedDataModelsEvents.Concat(efDomainEvents).ToList();
            _trackedEntities.Clear();

            if (!domainEvents.Any())
                break;

            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }
        }

        // Collect eviction tags from exactly what this commit will persist, after domain
        // handlers have added their changes but before the write. The outermost behavior
        // flushes them once the commit succeeds.
        await _evictionStrategy.CollectAsync(_context, cancellationToken);

        // Domain changes and domain events changes are all part of one transaction
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void TrackEntity(params AggregateRoot[] entities)
    {
        _trackedEntities.AddRange(entities);
    }
}
