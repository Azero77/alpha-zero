using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using MediatR;

namespace AlphaZero.Modules.Library.Application.DomainEventHandlers;

/// <summary>
/// Dispatches the producer-only <see cref="AccessCodeActivatedIntegrationEvent"/> via MassTransit outbox
/// when an access code is redeemed within the Library module (issue #58).
/// </summary>
public class AccessCodeActivatedDomainEventHandler(IPublishEndpoint bus)
    : INotificationHandler<AccessCodeRedeemedDomainEvent>
{
    public async Task Handle(AccessCodeRedeemedDomainEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = notification.TenantId != Guid.Empty
            ? notification.TenantId
            : (Guid.TryParse(notification.Resource.TenantIdString, out var parsedTenantId) ? parsedTenantId : Guid.Empty);

        await bus.Publish(new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            tenantId,
            notification.AccessCodeId,
            notification.UserId,
            notification.Resource,
            notification.RedeemedAt,
            notification.AuditReference), cancellationToken);
    }
}
