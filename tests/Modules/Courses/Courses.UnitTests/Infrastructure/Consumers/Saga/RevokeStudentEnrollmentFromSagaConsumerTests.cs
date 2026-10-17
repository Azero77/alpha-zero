using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;
using AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers.Saga;

/// <summary>
/// Regression protection for the Payments revocation-saga consumer (the money path, issue #34):
/// its Deactivate call now goes through the aggregate guards — a guard-pass publishes the saga's
/// success event; a guard-block (impossible domain move) publishes the revocation-failed event
/// the CourseRevocationSaga already surfaces into its <c>Failed</c> state.
/// </summary>
public class RevokeStudentEnrollmentFromSagaConsumerTests
{
    private readonly Mock<ICoursesModule> _coursesModuleMock = new();
    private readonly Mock<ILogger<RevokeStudentEnrollmentFromSagaConsumer>> _loggerMock = new();
    private readonly Mock<ConsumeContext<RevokeStudentEnrollmentFromSagaCommand>> _contextMock = new();
    private readonly RevokeStudentEnrollmentFromSagaConsumer _consumer;

    public RevokeStudentEnrollmentFromSagaConsumerTests()
    {
        _consumer = new RevokeStudentEnrollmentFromSagaConsumer(_coursesModuleMock.Object, _loggerMock.Object);
    }

    private RevokeStudentEnrollmentFromSagaCommand NewCommand() =>
        new(CorrelationId: Guid.NewGuid(), UserId: Guid.NewGuid(), CourseId: Guid.NewGuid());

    [Fact]
    public async Task Consume_Should_PublishRevokedEvent_WhenDeactivateCommandSucceeds()
    {
        // Arrange
        var command = NewCommand();
        _contextMock.Setup(x => x.Message).Returns(command);
        _coursesModuleMock
            .Setup(x => x.Send(It.IsAny<DeactivateEnrollmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorOr.Result.Success);

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(It.Is<StudentEnrollmentRevokedFromSagaEvent>(e => e.CorrelationId == command.CorrelationId), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.Publish(It.IsAny<StudentEnrollmentRevocationFailedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_PublishRevocationFailedEvent_WhenGuardsBlockTheMove()
    {
        // Arrange: the aggregate guard rejects the revocation (e.g. finalization race), the command
        // surfaces a domain conflict — the saga must see its failure event, never a false success.
        var command = NewCommand();
        _contextMock.Setup(x => x.Message).Returns(command);
        _coursesModuleMock
            .Setup(x => x.Send(It.IsAny<DeactivateEnrollmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorOr.Error.Conflict("Enrollment.InvalidTransition", "blocked"));

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(It.Is<StudentEnrollmentRevocationFailedEvent>(e => e.CorrelationId == command.CorrelationId), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.Publish(It.IsAny<StudentEnrollmentRevokedFromSagaEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_PublishRevocationFailedEvent_WhenEnrollmentNotFound()
    {
        // Arrange
        var command = NewCommand();
        _contextMock.Setup(x => x.Message).Returns(command);
        _coursesModuleMock
            .Setup(x => x.Send(It.IsAny<DeactivateEnrollmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorOr.Error.NotFound("Enrollment.NotFound", "not found"));

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(It.Is<StudentEnrollmentRevocationFailedEvent>(e => e.CorrelationId == command.CorrelationId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
