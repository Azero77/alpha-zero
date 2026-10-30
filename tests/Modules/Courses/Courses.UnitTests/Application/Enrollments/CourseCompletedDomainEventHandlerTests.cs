using AlphaZero.Modules.Courses.Application.Enrollments.EventHandlers;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using FluentAssertions;
using MassTransit;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

// #32: the domain→integration bridge. The handler mirrors the ItemCompleted/EnrollmentCreated
// path on the module's outbox conventions. The asserted behavior IS the contract:
// what event goes onto the bus, with which payload.
public class CourseCompletedDomainEventHandlerTests
{
    private static CourseCompletedDomainEvent CreateEvent() =>
        new(
            enrollmentId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            studentId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            completedOnUtc: new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Handle_Should_PublishCourseCompletedIntegrationEvent_WithMatchingPayload()
    {
        // Arrange
        var moduleBus = Substitute.For<IPublishEndpoint>();
        var handler = new DomainEventHandler(moduleBus);
        var domainEvent = CreateEvent();

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await moduleBus.Received(1).Publish(
            Arg.Is<CourseCompletedIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.CourseId == domainEvent.CourseId &&
                e.StudentId == domainEvent.StudentId &&
                e.TenantId == domainEvent.TenantId &&
                e.CompletedOnUtc == domainEvent.CompletedOnUtc),
            Arg.Any<CancellationToken>());
    }
}
