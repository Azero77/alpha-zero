using System.Text.Json;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.IntegrationEvents;

/// <summary>
/// Published when an access code is redeemed in the Library module (producer-only contract, issue #58).
/// Downstream modules (e.g. Courses) consume this contract to provision course access asynchronously.
/// Metadata carries per-resource-type provisioning hints (e.g. Courses' {"plan"} selector);
/// a null metadata is a valid resource contract that needs none.
/// </summary>
public record AccessCodeActivatedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    Guid AccessCodeId,
    Guid RedeemingUserId,
    ResourceArn TargetResourceArn,
    DateTime RedemptionTimestamp,
    Guid? AuditReference,
    JsonDocument? Metadata = null)
{
    public Guid UserId => RedeemingUserId;
    public ResourceArn Resource => TargetResourceArn;
    public DateTime RedeemedAt => RedemptionTimestamp;
}
