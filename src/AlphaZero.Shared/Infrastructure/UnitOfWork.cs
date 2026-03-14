using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure;

public class UnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    TContext _context;
    IPublisher _publisher;

    public UnitOfWork(TContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var entities = _context.ChangeTracker
            .Entries<AggregateRoot>()
            .ToList()
            ;

        var domainEvents = entities
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

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
