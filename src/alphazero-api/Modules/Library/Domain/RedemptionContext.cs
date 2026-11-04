using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Encapsulates the context and identity fields of an access code redemption event.
/// </summary>
public record RedemptionContext(
    Guid TenantId,
    Guid? LibraryId,
    Guid RedeemedByUserId,
    string StrategyId,
    ResourceArn TargetResourceArn,
    DateTime RedeemedAt,
    string? IpAddress = null,
    string? DeviceFingerprint = null)
{
    public Guid UserId => RedeemedByUserId;
}
