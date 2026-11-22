namespace AlphaZero.Modules.Payments.IntegrationEvents;

/// <summary>Durable Payments-owned request for Courses to grant Purchase-sourced access.</summary>
public sealed record PurchaseActivatedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    Guid PurchaseId,
    Guid UserId,
    string TargetResourceArn,
    string Metadata,
    Guid AuditReference);
