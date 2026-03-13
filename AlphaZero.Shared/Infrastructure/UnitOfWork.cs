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

    public async Task<int> SaveChangesAsync()
    {
        var domainEvents = _context.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            ;
        if (domainEvents is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent);
            }
        }
        return await _context.SaveChangesAsync();
    }
}
