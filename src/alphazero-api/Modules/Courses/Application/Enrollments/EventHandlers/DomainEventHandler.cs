using MassTransit;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Application;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollments.EventHandlers;

public class DomainEventHandler :
    INotificationHandler<ItemCompletedDomainEvent>,
    INotificationHandler<EnrollmentCreatedDomainEvent>,
    INotificationHandler<EnrollmentSuspendedDomainEvent>,
    INotificationHandler<EnrollmentResumedDomainEvent>,
    INotificationHandler<EnrollmentExpiredDomainEvent>
{
    private readonly IPublishEndpoint _moduleBus;

    public DomainEventHandler(IPublishEndpoint moduleBus)
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

    public async Task Handle(EnrollmentCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new EnrollmentCreatedIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId
        ), cancellationToken);
    }

    public async Task Handle(EnrollmentSuspendedDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new EnrollmentSuspendedIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId,
            notification.Reason
        ), cancellationToken);
    }

    public async Task Handle(EnrollmentResumedDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new EnrollmentResumedIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId
        ), cancellationToken);
    }

    public async Task Handle(EnrollmentExpiredDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new EnrollmentExpiredIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId
        ), cancellationToken);
    }
}
