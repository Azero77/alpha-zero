using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class LibraryAuditLogRepository : ILibraryAuditLogRepository
{
    private readonly AppDbContext _context;

    public LibraryAuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LibraryAuditLog entry, CancellationToken cancellationToken = default)
    {
        // Outbox idempotency: check if event already recorded in context or DB to avoid duplicate tracking
        if (_context.LibraryAuditLogs.Local.Any(x => x.EventId == entry.EventId))
        {
            return;
        }

        var exists = await _context.LibraryAuditLogs
            .IgnoreQueryFilters()
            .AnyAsync(x => x.EventId == entry.EventId, cancellationToken);

        if (exists)
        {
            return;
        }

        await _context.LibraryAuditLogs.AddAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryAuditLog>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        return await _context.LibraryAuditLogs
            .AsNoTracking()
            .Where(x => x.AggregateId == aggregateId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryAuditLog>> ListAsync(
        Guid tenantId,
        Guid? libraryId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LibraryAuditLogs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (libraryId.HasValue)
        {
            query = query.Where(x => x.LibraryId == libraryId.Value);
        }

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Guid tenantId,
        Guid? libraryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LibraryAuditLogs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (libraryId.HasValue)
        {
            query = query.Where(x => x.LibraryId == libraryId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }
}
