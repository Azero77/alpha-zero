using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure;

public class UnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    TContext _context;
    IPublisher _publisher;
    IEnumerable<ITrackedRepository> _trackedRepositories;

    public UnitOfWork(TContext context, IPublisher publisher, IEnumerable<ITrackedRepository> trackedRepositories)
    {
        _context = context;
        _publisher = publisher;
        _trackedRepositories = trackedRepositories;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        // 1. Flush DataModel repositories → push Add/Update/Remove to DbContext
        foreach (var repo in _trackedRepositories)
        {
            await repo.FlushAsync(cancellationToken);
        }

        // 2. Harvest domain events from DataModel-tracked entities
        var trackedDomainEvents = _trackedRepositories
            .SelectMany(r => r.GetTrackedEntries())
            .Where(e => e.DomainEntity is AggregateRoot)
            .Select(e => (AggregateRoot)e.DomainEntity)
            .SelectMany(ar => ar.PopDomainEvents())
            .ToList();

        // 3. Harvest domain events from EF-tracked entities (existing path)
        var efDomainEvents = _context.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        var domainEvents = trackedDomainEvents.Concat(efDomainEvents).ToList();

        if (domainEvents is not null
            &&
            domainEvents.Any())
        {
            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent,cancellationToken);
            }
        }
        //here we are saving after the domain events run, we see that domain changes and domain events changes are all one part of a transaction
        //and everything exceeds the bounded context should be raised by the application layer and handled by an outbox and then a background service to publish the integration event
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
