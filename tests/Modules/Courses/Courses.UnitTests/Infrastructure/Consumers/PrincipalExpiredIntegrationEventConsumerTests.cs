using AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal;
using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using AlphaZero.Modules.Identity.IntegrationEvents;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers;

/// <summary>
/// Step 8 consumer plumbing (issue #34 rework §3.4 path a): the course ARN is parsed from the
/// event's resource path and an application command is dispatched through the module scope; all
/// deterministic no-work shapes ACK (no redelivery) — only unexpected failures throw.
/// </summary>
public class PrincipalExpiredIntegrationEventConsumerTests
{
    private readonly Mock<ICoursesModule> _coursesModuleMock = new();
    private readonly PrincipalExpiredIntegrationEventConsumer _consumer;

    public PrincipalExpiredIntegrationEventConsumerTests()
    {
        _consumer = new PrincipalExpiredIntegrationEventConsumer(
            _coursesModuleMock.Object,
            new Mock<ILogger<PrincipalExpiredIntegrationEventConsumer>>().Object);
    }

    private static ConsumeContext<PrincipalExpiredIntegrationEvent> Ctx(PrincipalExpiredIntegrationEvent message)
    {
        var mock = new Mock<ConsumeContext<PrincipalExpiredIntegrationEvent>>();
        mock.Setup(c => c.Message).Returns(message);
        return mock.Object;
    }

    private static PrincipalExpiredIntegrationEvent Event(string resourcePath)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), resourcePath);

    [Fact]
    public async Task Consume_Should_DispatchMarkCommand_WithCourseFromArn()
    {
        var courseId = Guid.NewGuid();
        var message = Event($"course/{courseId}");
        _coursesModuleMock.Setup(m => m.Send(It.IsAny<MarkEnrollmentExpiredFromPrincipalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        await _consumer.Consume(Ctx(message));

        _coursesModuleMock.Verify(m => m.Send(
            It.Is<MarkEnrollmentExpiredFromPrincipalCommand>(c => c.CourseId == courseId && c.UserId == message.UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldDispatchMarkCommand_WhenArnCoversACourseNestedResource()
    {
        // section/lesson/assessment/coursevideo ARNs all begin course/{id} — the course is still resolvable.
        var courseId = Guid.NewGuid();
        var message = new PrincipalExpiredIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"course/{courseId}/video/{Guid.NewGuid()}");
        _coursesModuleMock.Setup(m => m.Send(It.IsAny<MarkEnrollmentExpiredFromPrincipalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        await _consumer.Consume(Ctx(message));

        _coursesModuleMock.Verify(m => m.Send(
            It.Is<MarkEnrollmentExpiredFromPrincipalCommand>(c => c.CourseId == courseId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_AckWithoutDispatch_WhenResourcePathIsNotCourseScoped()
    {
        var message = Event($"user/{Guid.NewGuid()}"); // the operator is course-agnostic; other services send no work here

        await _consumer.Consume(Ctx(message));

        _coursesModuleMock.Verify(m => m.Send(It.IsAny<MarkEnrollmentExpiredFromPrincipalCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_AckDeterministicNoWorkStates_WithoutThrowing()
    {
        var message = Event($"course/{Guid.NewGuid()}");
        _coursesModuleMock.Setup(m => m.Send(It.IsAny<MarkEnrollmentExpiredFromPrincipalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound("Enrollment.NotFound", "not visible"));

        await _consumer.Consume(Ctx(message));

        // no exception → MassTransit ACKs; duplicate evaluations stay cheap (storm-safety)

        var transitionGuardBlock = Event($"course/{Guid.NewGuid()}");
        _coursesModuleMock.Setup(m => m.Send(It.IsAny<MarkEnrollmentExpiredFromPrincipalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Conflict("Enrollment.InvalidTransition", "cannot expire an Inactive enrollment"));

        await _consumer.Consume(Ctx(transitionGuardBlock));
    }

    [Fact]
    public async Task Consume_Should_Throw_WhenTheDispatchFailsUnexpectedly()
    {
        var message = Event($"course/{Guid.NewGuid()}");
        _coursesModuleMock.Setup(m => m.Send(It.IsAny<MarkEnrollmentExpiredFromPrincipalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure("Some.InfrastructureError", "boom"));

        var act = async () => await _consumer.Consume(Ctx(message));

        await act.Should().ThrowAsync<InvalidOperationException>() // MassTransit redelivers
            .Where(ex => ex.Message.Contains("MarkEnrollmentExpiredFromPrincipal failed"));
    }
}
