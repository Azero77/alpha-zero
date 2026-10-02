using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using AlphaZero.Modules.Library.Infrastructure.Persistance;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class RedemptionAuditLogRepository : IRedemptionAuditLogRepository
{
    private readonly AppDbContext _context;

    public RedemptionAuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RedemptionAuditLog log, CancellationToken ct = default)
        => await _context.RedemptionAuditLogs.AddAsync(log, ct);

    public async Task<PagedResult<RedemptionAuditLog>> GetPagedAsync(
        Guid? libraryId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.RedemptionAuditLogs.AsNoTracking();

        if (libraryId.HasValue)
            query = query.Where(x => x.LibraryId == libraryId);

        if (from.HasValue)
        {
            var fromDate = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.RedeemedAt >= fromDate);
        }

        if (to.HasValue)
        {
            var toDate = to.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(x => x.RedeemedAt <= toDate);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.RedeemedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RedemptionAuditLog>(items, total, page, pageSize);
    }
}
