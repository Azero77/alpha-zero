namespace AlphaZero.Modules.Payments.IntegrationEvents;

public sealed record PurchaseActivationMetadata(
    Guid ItemId,
    string ItemType,
    string ItemTitle);

/// <summary>Durable Payments-owned request for Courses to grant Purchase-sourced access.</summary>
public sealed record PurchaseActivatedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    Guid PurchaseId,
    Guid UserId,
    string TargetResourceArn,
    PurchaseActivationMetadata Metadata,
    Guid AuditReference);

public sealed record PurchaseAccessRevocationRequestedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    Guid PurchaseId,
    Guid UserId,
    string TargetResourceArn,
    string CauseType,
    Guid CauseId,
    Guid AuditReference);
