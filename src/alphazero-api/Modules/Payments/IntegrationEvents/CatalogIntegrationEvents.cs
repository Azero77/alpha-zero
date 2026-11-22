namespace AlphaZero.Modules.Payments.IntegrationEvents;

/// <summary>Raised by the Payments mirror consumer for reconciliation and observability.</summary>
public sealed record CatalogProductMirrorAppliedIntegrationEvent(Guid ProductId, Guid TenantId, long SourceVersion);
