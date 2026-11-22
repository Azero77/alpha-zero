using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.EventHandlers;

public sealed class CoursePlanCatalogChangedDomainEventHandler(IPublishEndpoint moduleBus)
    : INotificationHandler<CoursePlanCatalogChangedDomainEvent>
{
    public Task Handle(CoursePlanCatalogChangedDomainEvent notification, CancellationToken cancellationToken) =>
        moduleBus.Publish(new CoursePlanAvailabilityChangedIntegrationEvent(
            notification.Id,
            notification.TenantId,
            notification.CourseId,
            notification.CoursePlanId,
            notification.TargetResourceArn,
            notification.Title,
            notification.Availability == CoursePlanCatalogAvailability.Available
                ? CoursePlanAvailability.Available
                : CoursePlanAvailability.Unavailable,
            notification.SourceVersion,
            notification.OccurredAt), cancellationToken);
}
