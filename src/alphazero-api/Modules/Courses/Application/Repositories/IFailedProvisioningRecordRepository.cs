using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IFailedProvisioningRecordRepository
{
    Task AddAsync(FailedProvisioningRecord record, CancellationToken cancellationToken = default);
    Task<FailedProvisioningRecord?> GetByAccessCodeIdAsync(Guid accessCodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FailedProvisioningRecord>> GetFailedRecordsAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
