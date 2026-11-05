using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.IntegrationEvents;

/// <summary>
/// Published when an access code is redeemed in the Library module (producer-only contract, issue #58).
/// Downstream modules (e.g. Courses) consume this contract to provision course access asynchronously.
/// </summary>
public record AccessCodeActivatedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    Guid AccessCodeId,
    Guid RedeemingUserId,
    ResourceArn TargetResourceArn,
    DateTime RedemptionTimestamp,
    Guid? AuditReference)
{
    public Guid UserId => RedeemingUserId;
    public ResourceArn Resource => TargetResourceArn;
    public DateTime RedeemedAt => RedemptionTimestamp;
}
