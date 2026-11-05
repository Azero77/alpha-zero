using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class FailedProvisioningRecordRepository : IFailedProvisioningRecordRepository
{
    private readonly AppDbContext _context;

    public FailedProvisioningRecordRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(FailedProvisioningRecord record, CancellationToken cancellationToken = default)
    {
        await _context.FailedProvisioningRecords.AddAsync(record, cancellationToken);
    }

    public async Task<FailedProvisioningRecord?> GetByAccessCodeIdAsync(Guid accessCodeId, CancellationToken cancellationToken = default)
    {
        return await _context.FailedProvisioningRecords
            .FirstOrDefaultAsync(r => r.AccessCodeId == accessCodeId, cancellationToken);
    }

    public async Task<IReadOnlyList<FailedProvisioningRecord>> GetFailedRecordsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FailedProvisioningRecords
            .Where(r => r.Status == ProvisioningStatus.Failed)
            .OrderByDescending(r => r.LastAttemptedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
