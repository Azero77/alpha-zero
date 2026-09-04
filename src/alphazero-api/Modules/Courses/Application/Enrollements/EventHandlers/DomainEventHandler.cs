using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Application;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollements.EventHandlers;

public class DomainEventHandler : 
    INotificationHandler<ItemCompletedDomainEvent>,
    INotificationHandler<EnrollementCreatedDomainEvent>
{
    private readonly IModuleBus _moduleBus;

    public DomainEventHandler(IModuleBus moduleBus)
    {
        _moduleBus = moduleBus;
    }

    public async Task Handle(ItemCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new ItemCompletedIntegrationEvent(
            notification.EnrollmentId,
            notification.CourseId,
            notification.BitIndex,
            notification.OldCompletionPercentage,
            notification.NewCompletionPercentage
        ), cancellationToken);
    }

    public async Task Handle(EnrollementCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new EnrollementCreatedIntegrationEvent(
            notification.EnrollementId,
            notification.StudentId,
            notification.CourseId
        ), cancellationToken);
    }
}
