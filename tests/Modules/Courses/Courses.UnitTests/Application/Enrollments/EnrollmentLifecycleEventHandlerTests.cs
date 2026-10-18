using Moq;
using AlphaZero.Modules.Courses.Application.Enrollments.EventHandlers;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using FluentAssertions;
using MassTransit;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

/// <summary>
/// Lifecycle domain events map 1:1 to their integration events on the module bus
/// (issue #34 story #7 — consumers subscribe to resumption/suspension/expiry the same
/// way they already react to creation/completion).
/// </summary>
public class EnrollmentLifecycleEventHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_PublishSuspendedIntegrationEvent_WithReason()
    {
        var bus = new Mock<IPublishEndpoint>();
        var handler = new DomainEventHandler(bus.Object);
        var domainEvent = new EnrollmentSuspendedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, "billing dispute");

        await handler.Handle(domainEvent, CancellationToken.None);

        bus.Verify(b => b.Publish(
            It.Is<EnrollmentSuspendedIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.StudentId == domainEvent.StudentId &&
                e.CourseId == domainEvent.CourseId &&
                e.TenantId == TenantId &&
                e.Reason == "billing dispute"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_PublishResumedIntegrationEvent()
    {
        var bus = new Mock<IPublishEndpoint>();
        var handler = new DomainEventHandler(bus.Object);
        var domainEvent = new EnrollmentResumedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId);

        await handler.Handle(domainEvent, CancellationToken.None);

        bus.Verify(b => b.Publish(
            It.Is<EnrollmentResumedIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.StudentId == domainEvent.StudentId &&
                e.CourseId == domainEvent.CourseId &&
                e.TenantId == TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_PublishExpiredIntegrationEvent()
    {
        var bus = new Mock<IPublishEndpoint>();
        var handler = new DomainEventHandler(bus.Object);
        var domainEvent = new EnrollmentExpiredDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId);

        await handler.Handle(domainEvent, CancellationToken.None);

        bus.Verify(b => b.Publish(
            It.Is<EnrollmentExpiredIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.StudentId == domainEvent.StudentId &&
                e.CourseId == domainEvent.CourseId &&
                e.TenantId == TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
