using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class SaleRecordRepository : ISaleRecordRepository
{
    private readonly AppDbContext _context;

    public SaleRecordRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SaleRecord saleRecord, CancellationToken cancellationToken = default)
    {
        await _context.SaleRecords.AddAsync(saleRecord, cancellationToken);
    }

    public async Task<bool> ExistsForCodeAsync(Guid accessCodeId, CancellationToken cancellationToken = default)
    {
        return await _context.SaleRecords
            .AnyAsync(x => x.AccessCodeId == accessCodeId, cancellationToken);
    }

    public async Task<SaleRecord?> GetByAccessCodeIdAsync(Guid accessCodeId, CancellationToken cancellationToken = default)
    {
        return await _context.SaleRecords
            .FirstOrDefaultAsync(x => x.AccessCodeId == accessCodeId, cancellationToken);
    }

    public async Task<int> CountByLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        return await _context.SaleRecords
            .CountAsync(x => x.LibraryId == libraryId, cancellationToken);
    }

    public async Task<decimal> GetRevenueByLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        return await _context.SaleRecords
            .Where(x => x.LibraryId == libraryId)
            .SumAsync(x => x.Price, cancellationToken);
    }
}
