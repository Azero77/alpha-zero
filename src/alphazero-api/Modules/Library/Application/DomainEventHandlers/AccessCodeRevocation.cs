using MassTransit;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using MediatR;

namespace AlphaZero.Modules.Library.Application.DomainEventHandlers;

public class AccessCodeVoidedDomainEventHandler(IPublishEndpoint bus, IClock clock) : INotificationHandler<AccessCodeVoidedDomainEvent>
{
    public async Task Handle(AccessCodeVoidedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Only publish revocation integration event if the code was redeemed by a user
        if (notification.UserId.HasValue)
        {
            var tenantId = notification.TenantId != Guid.Empty
                ? notification.TenantId
                : (Guid.TryParse(notification.Resource.TenantIdString, out var parsedTenantId) ? parsedTenantId : Guid.Empty);

            await bus.Publish(new AccessCodeRevokedIntegrationEvent(
                Guid.NewGuid(),
                notification.AccessCodeId,
                notification.UserId.Value,
                notification.Resource,
                clock.Now,
                tenantId), cancellationToken);
        }
    }
}
