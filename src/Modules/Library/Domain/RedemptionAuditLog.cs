using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Represents an immutable audit record for an access code redemption.
/// </summary>
public class RedemptionAuditLog : Entity
{
    public Guid TenantId { get; private set; }
    public Guid? LibraryId { get; private set; }   // null = admin code
    public Guid AccessCodeId { get; private set; }
    public Guid RedeemedByUserId { get; private set; }
    public string StrategyId { get; private set; } = default!;
    public ResourceArn TargetResourceArn { get; private set; } = default!;
    public DateTime RedeemedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? DeviceFingerprint { get; private set; }

    private RedemptionAuditLog() { }

    private RedemptionAuditLog(
        Guid id,
        Guid tenantId,
        Guid? libraryId,
        Guid accessCodeId,
        Guid redeemedByUserId,
        string strategyId,
        ResourceArn targetResourceArn,
        DateTime redeemedAt,
        string? ipAddress = null,
        string? deviceFingerprint = null) : base(id)
    {
        TenantId = tenantId;
        LibraryId = libraryId;
        AccessCodeId = accessCodeId;
        RedeemedByUserId = redeemedByUserId;
        StrategyId = strategyId;
        TargetResourceArn = targetResourceArn;
        RedeemedAt = redeemedAt;
        IpAddress = ipAddress;
        DeviceFingerprint = deviceFingerprint;
    }

    public static RedemptionAuditLog Record(
        Guid tenantId,
        Guid? libraryId,
        Guid accessCodeId,
        Guid redeemedByUserId,
        string strategyId,
        ResourceArn targetResourceArn,
        DateTime redeemedAt,
        string? ipAddress = null,
        string? deviceFingerprint = null)
    {
        return new RedemptionAuditLog(
            Guid.NewGuid(),
            tenantId,
            libraryId,
            accessCodeId,
            redeemedByUserId,
            strategyId,
            targetResourceArn,
            redeemedAt,
            ipAddress,
            deviceFingerprint);
    }
}
