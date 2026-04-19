using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.IntegrationEvents;

public record AccessCodeRevokedIntegrationEvent(
    Guid EventId,
    Guid AccessCodeId,
    Guid UserId,
    ResourceArn Resource,
    DateTime OccuredOn);
