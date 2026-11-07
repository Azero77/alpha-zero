using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

public enum ProvisioningStatus
{
    Pending = 0,
    Provisioned = 1,
    Failed = 2
}

/// <summary>
/// Represents a parked failed-provisioning record when an access code activation event
/// fails domain validation or provisioning in the Courses module (issue #58).
/// Visible to the operations and fraud queues (#59) and retryable.
/// </summary>
public class FailedProvisioningRecord : TenantOwnedEntity
{
    public Guid AccessCodeId { get; private set; }
    public Guid UserId { get; private set; }
    public string TargetResourceArn { get; private set; } = string.Empty;
    public string FailureReason { get; private set; } = string.Empty;
    public ProvisioningStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastAttemptedAt { get; private set; }

    private FailedProvisioningRecord()
        : base(Guid.NewGuid(), Guid.Empty)
    {
    }

    public FailedProvisioningRecord(
        Guid id,
        Guid tenantId,
        Guid accessCodeId,
        Guid userId,
        string targetResourceArn,
        string failureReason,
        ProvisioningStatus status,
        int retryCount,
        DateTime createdAt,
        DateTime lastAttemptedAt)
        : base(id, tenantId)
    {
        AccessCodeId = accessCodeId;
        UserId = userId;
        TargetResourceArn = targetResourceArn;
        FailureReason = failureReason;
        Status = status;
        RetryCount = retryCount;
        CreatedAt = createdAt;
        LastAttemptedAt = lastAttemptedAt;
    }

    public static FailedProvisioningRecord Create(
        Guid tenantId,
        Guid accessCodeId,
        Guid userId,
        string targetResourceArn,
        string failureReason,
        DateTime? now = null)
    {
        var timestamp = now ?? DateTime.UtcNow;
        return new FailedProvisioningRecord(
            Guid.NewGuid(),
            tenantId,
            accessCodeId,
            userId,
            targetResourceArn,
            failureReason,
            ProvisioningStatus.Failed,
            1,
            timestamp,
            timestamp);
    }

    public void RecordRetryFailure(string failureReason, DateTime? now = null)
    {
        FailureReason = failureReason;
        RetryCount++;
        LastAttemptedAt = now ?? DateTime.UtcNow;
        Status = ProvisioningStatus.Failed;
    }

    public void MarkProvisioned(DateTime? now = null)
    {
        Status = ProvisioningStatus.Provisioned;
        LastAttemptedAt = now ?? DateTime.UtcNow;
    }
}
