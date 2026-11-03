using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Repositores;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace AlphaZero.Shared.Infrastructure;

public class UnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    TContext _context;
    IPublisher _publisher;
    private readonly List<AggregateRoot> _trackedEntities = new();
    public UnitOfWork(TContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
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

        // Eviction keys/tags are queued by the caching repository decorators when the aggregate is
        // written; EF Core interceptors below flush them at DB commit points.

        // Domain changes and domain events changes are all part of one transaction
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void TrackEntity(params AggregateRoot[] entities)
    {
        _trackedEntities.AddRange(entities);
    }
}

// Both interceptors hold no per-instance eviction state: the queues live on the request-scoped
// ScopedEvictionProvider instance, which decorators, the unit of work, the DbContext and both
// interceptors resolve from the SAME lifetime scope (all registered scoped). The queue must live
// on the shared instance, NOT in AsyncLocal: the EF interceptor fires from the MediatR pipeline
// level that called SaveChangesAsync, an ANCESTOR context of the command handler in which the
// decorators queued — and AsyncLocal writes never flow back up to an ancestor context.
public class EvictionSaveChangesInterceptor(IEvictionProvider evictionProvider) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        // Flush ONLY when this save was genuinely standalone:
        //  A. No explicit EF transaction → flush immediately (the write is committed once this returns).
        //  B. Inside an explicit EF transaction → do nothing; the transaction interceptor flushes at commit.
        //  C. Inside an ambient System.Transactions transaction → fail fast: an ambient commit has no
        //     EF hook, so queued keys could never be flushed — silently serving stale cache. Nothing in
        //     the repo opens no ambient System.Transactions transaction (Npgsql connections are
        //     opened with Enlist=false), so this guard is the invariant that keeps the write path
        //     cache-safe.
        if (System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException(
                "A save ran inside an ambient System.Transactions.Transaction, which has no EF commit hook " +
                "to flush cache eviction. Open an EF transaction (BeginTransaction) instead — ambient " +
                "transactions are not supported by the cache eviction pipeline.");
        }

        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            await evictionProvider.FlushAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // Failed standalone save: burn exactly the keys that failed save queued (staging-free Discard).
        // Inside an EF transaction the queued keys survive for the commit/rollback interceptor to decide.
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            evictionProvider.Discard();
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}

public class EvictionTransactionInterceptor(IEvictionProvider evictionProvider) : DbTransactionInterceptor
{
    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = new CancellationToken())
    {
        await evictionProvider.FlushAsync(cancellationToken);
        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = new CancellationToken())
    {
        evictionProvider.Discard();
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }
    public override void TransactionRolledBack(
        DbTransaction transaction,
        TransactionEndEventData eventData)
    {
        evictionProvider.Discard();
        base.TransactionRolledBack(transaction, eventData);
    }

    // --- TRANSACTION ERROR (e.g., Ambiguous Commit Failure) ---
    public override async Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Action == "Commit")
        {
            // If failure happened during Commit, the DB might have actually committed.
            // Defensively flush to prevent stale cache (over-eviction is always safe).
            await evictionProvider.FlushAsync(CancellationToken.None);
        }
        else
        {
            evictionProvider.Discard();
        }

        await base.TransactionFailedAsync(transaction, eventData, cancellationToken);
    }
}