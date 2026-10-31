using MassTransit;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Audits;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollments.EventHandlers;

/// <summary>
/// In-process lifecycle event handler (issue #34 §3.1):
/// 1. TRANSITION events first stage one <see cref="EnrollmentAuditEntry"/> row on the shared module
///    context — the UnitOfWork publishes notification handlers BEFORE its single SaveChangesAsync,
///    so audit rows commit atomically with the aggregate mutation (no bus, no outbox dependency on
///    the write; §5 rule 7).
/// 2. Then lifecycle events are reflected 1:1 onto integration events on the module bus, exactly as
///    before. Deactivation stays integration-event-less on purpose: the
///    <see cref="EnrollmentDeactivatedDomainEvent"/> exists for the audit writer's eyes only (the
///    Payments revocation flow keeps its identity).
/// </summary>
public class DomainEventHandler :
    INotificationHandler<ItemCompletedDomainEvent>,
    INotificationHandler<EnrollmentCreatedDomainEvent>,
    INotificationHandler<EnrollmentSuspendedDomainEvent>,
    INotificationHandler<EnrollmentResumedDomainEvent>,
    INotificationHandler<EnrollmentExpiredDomainEvent>,
    INotificationHandler<EnrollmentDeactivatedDomainEvent>,
    INotificationHandler<CourseCompletedDomainEvent>
{
    private readonly IPublishEndpoint _moduleBus;
    private readonly IEnrollmentAuditRepository _audit;

    public DomainEventHandler(IPublishEndpoint moduleBus, IEnrollmentAuditRepository audit)
    {
        _moduleBus = moduleBus;
        _audit = audit;
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
        // No audit row here: creation is not a transition — EnrolledOn already carries who/when.
        await _moduleBus.Publish(new EnrollmentCreatedIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId
        ), cancellationToken);
    }

    public async Task Handle(EnrollmentSuspendedDomainEvent notification, CancellationToken cancellationToken)
    {
        await StageAuditAsync(
            notification, EnrollmentAuditEventKind.Suspended, EnrollmentStatus.Suspended,
            notification.Reason, cancellationToken);

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
        await StageAuditAsync(
            notification, EnrollmentAuditEventKind.Resumed, EnrollmentStatus.Active,
            cause: null, cancellationToken);

        await _moduleBus.Publish(new EnrollmentResumedIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId
        ), cancellationToken);
    }

    public async Task Handle(EnrollmentExpiredDomainEvent notification, CancellationToken cancellationToken)
    {
        await StageAuditAsync(
            notification, EnrollmentAuditEventKind.Expired, EnrollmentStatus.Expired,
            cause: null, cancellationToken);

        await _moduleBus.Publish(new EnrollmentExpiredIntegrationEvent(
            notification.EnrollmentId,
            notification.StudentId,
            notification.CourseId,
            notification.TenantId
        ), cancellationToken);
    }

    public async Task Handle(EnrollmentDeactivatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Audit-only by design (§3.1): no integration event for deactivation on the module bus.
        await StageAuditAsync(
            notification, EnrollmentAuditEventKind.Deactivated, EnrollmentStatus.Inactive,
            cause: null, cancellationToken);
    }

    public async Task Handle(CourseCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        await _moduleBus.Publish(new CourseCompletedIntegrationEvent(
            notification.EnrollmentId,
            notification.CourseId,
            notification.StudentId,
            notification.TenantId,
            notification.CompletedOnUtc
        ), cancellationToken);
    }

    private Task StageAuditAsync<TEvent>(
        TEvent notification,
        EnrollmentAuditEventKind kind,
        EnrollmentStatus to,
        string? cause,
        CancellationToken cancellationToken)
        where TEvent : DomainEvent, IEnrollmentTransitionEvent
        => _audit.AddAsync(EnrollmentAuditEntry.Create(
            notification.TenantId,
            notification.EnrollmentId,
            kind,
            notification.ActorId,
            notification.OccuredOn,
            EnrollmentAuditMetadata.FromTransition(notification.FromStatus, to, cause)),
            cancellationToken);
}
