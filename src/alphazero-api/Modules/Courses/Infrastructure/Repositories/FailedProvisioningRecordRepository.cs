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

    public async Task<FailedProvisioningRecord?> GetByTenantAndAccessCodeAsync(
        Guid tenantId,
        Guid accessCodeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.FailedProvisioningRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.AccessCodeId == accessCodeId, cancellationToken);
    }

    public async Task ParkFailureAsync(
        Guid tenantId,
        Guid accessCodeId,
        Guid userId,
        string targetResourceArn,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsRelational())
        {
            // One atomic upsert instead of check-then-insert: the unique (TenantId, AccessCodeId)
            // index makes concurrent consumers (at-least-once redelivery) converge onto a single
            // row — recording this delivery as one more retry — with no insert race and no
            // aborted-transaction recovery path. Mirrors FailedProvisioningRecord.Create /
            // RecordRetryFailure (Status 'Failed', RetryCount 1 first park / +1 on re-park);
            // keep the literals in sync with the HasConversion<string>() enum.
            var now = DateTime.UtcNow;
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Courses"."FailedProvisioningRecords"
                    ("Id", "TenantId", "AccessCodeId", "UserId", "TargetResourceArn", "FailureReason",
                     "Status", "RetryCount", "CreatedAt", "LastAttemptedAt")
                VALUES ({Guid.NewGuid()}, {tenantId}, {accessCodeId}, {userId}, {targetResourceArn}, {failureReason},
                        'Failed', 1, {now}, {now})
                ON CONFLICT ("TenantId", "AccessCodeId") DO UPDATE SET
                    "FailureReason" = EXCLUDED."FailureReason",
                    "RetryCount" = "FailedProvisioningRecords"."RetryCount" + 1,
                    "Status" = 'Failed',
                    "LastAttemptedAt" = EXCLUDED."LastAttemptedAt";
                """, cancellationToken);
            return;
        }

        // Non-relational provider (EF InMemory in unit tests): cannot execute raw SQL — fall
        // back to the EF-native path. Concurrency there is sequential (single consumer), so the
        // check-then-insert race this avoids is not exercisable and needs no upsert.
        var existing = await GetByTenantAndAccessCodeAsync(tenantId, accessCodeId, cancellationToken);

        if (existing != null)
        {
            existing.RecordRetryFailure(failureReason);
        }
        else
        {
            _context.FailedProvisioningRecords.Add(
                FailedProvisioningRecord.Create(tenantId, accessCodeId, userId, targetResourceArn, failureReason));
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkProvisionedIfExistsAsync(
        Guid tenantId,
        Guid accessCodeId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByTenantAndAccessCodeAsync(tenantId, accessCodeId, cancellationToken);

        if (existing != null && existing.Status != ProvisioningStatus.Provisioned)
        {
            existing.MarkProvisioned();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<FailedProvisioningRecord>> GetFailedRecordsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FailedProvisioningRecords
            .Where(r => r.Status == ProvisioningStatus.Failed)
            .OrderByDescending(r => r.LastAttemptedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FailedProvisioningRecord>> GetUnprovisionedByTenantAndAccessCodesAsync(
        Guid tenantId,
        IReadOnlyList<Guid> accessCodeIds,
        CancellationToken cancellationToken = default)
    {
        var ids = accessCodeIds.ToList();

        return await _context.FailedProvisioningRecords
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && r.Status != ProvisioningStatus.Provisioned
                        && ids.Contains(r.AccessCodeId))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
