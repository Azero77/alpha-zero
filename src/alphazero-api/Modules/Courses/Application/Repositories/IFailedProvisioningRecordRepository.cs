using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IFailedProvisioningRecordRepository
{
    Task AddAsync(FailedProvisioningRecord record, CancellationToken cancellationToken = default);
    Task<FailedProvisioningRecord?> GetByAccessCodeIdAsync(Guid accessCodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FailedProvisioningRecord>> GetFailedRecordsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Tenant-scoped lookup. All provisioning paths are tenant-isolated — a record
    /// belonging to another tenant must never match.
    /// </summary>
    Task<FailedProvisioningRecord?> GetByTenantAndAccessCodeAsync(
        Guid tenantId,
        Guid accessCodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically records a provisioning failure for (tenantId, accessCodeId):
    /// inserts a Failed record on first park, otherwise bumps RetryCount and
    /// refreshes FailureReason/LastAttemptedAt. Converges concurrent redeliveries
    /// onto a single row via the unique (TenantId, AccessCodeId) index.
    /// </summary>
    Task ParkFailureAsync(
        Guid tenantId,
        Guid accessCodeId,
        Guid userId,
        string targetResourceArn,
        string failureReason,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the existing record (if any) Provisioned. No-op when absent.</summary>
    Task MarkProvisionedIfExistsAsync(
        Guid tenantId,
        Guid accessCodeId,
        CancellationToken cancellationToken = default);
}
