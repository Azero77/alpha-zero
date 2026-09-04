using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using MediatR;

namespace AlphaZero.Modules.Library.Application.DomainEventHandlers;

public class AccessCodeVoidedDomainEventHandler(IModuleBus bus, IClock clock) : INotificationHandler<AccessCodeVoidedDomainEvent>
{
    public async Task Handle(AccessCodeVoidedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Notify other modules that this resource access must be revoked
        await bus.Publish(new AccessCodeRevokedIntegrationEvent(
            Guid.NewGuid(),
            notification.AccessCodeId,
            notification.UserId,
            notification.Resource,
            clock.Now));
    }
}
