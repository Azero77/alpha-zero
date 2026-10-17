using AlphaZero.Modules.Courses.Application;
using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Deactivate;
using AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;
using AlphaZero.Modules.Courses.IntegrationEvents;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers.Saga;

public class RevokeStudentEnrollmentFromSagaConsumerTests
{
    private readonly Mock<ICoursesModule> _coursesModuleMock;
    private readonly Mock<ILogger<RevokeStudentEnrollmentFromSagaConsumer>> _loggerMock;
    private readonly Mock<ConsumeContext<RevokeStudentEnrollmentFromSagaCommand>> _contextMock;
    private readonly RevokeStudentEnrollmentFromSagaConsumer _consumer;

    public RevokeStudentEnrollmentFromSagaConsumerTests()
    {
        _coursesModuleMock = new Mock<ICoursesModule>();
        _loggerMock = new Mock<ILogger<RevokeStudentEnrollmentFromSagaConsumer>>();
        _contextMock = new Mock<ConsumeContext<RevokeStudentEnrollmentFromSagaCommand>>();
        _consumer = new RevokeStudentEnrollmentFromSagaConsumer(_coursesModuleMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_Should_PublishRevokedEvent_WhenDeactivationSucceeds()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var command = new RevokeStudentEnrollmentFromSagaCommand(correlationId, userId, courseId);
        _contextMock.Setup(x => x.Message).Returns(command);

        _coursesModuleMock
            .Setup(x => x.Send(It.IsAny<DeactivateEnrollmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(
            It.Is<DeactivateEnrollmentCommand>(cmd => cmd.UserId == userId && cmd.CourseId == courseId),
            It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.Publish(
            It.Is<StudentEnrollmentRevokedFromSagaEvent>(e => e.CorrelationId == correlationId),
            It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.Publish(
            It.IsAny<StudentEnrollmentRevocationFailedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_PublishFailureEvent_WhenDeactivationFails()
    {
        // Arrange
        var command = new RevokeStudentEnrollmentFromSagaCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _contextMock.Setup(x => x.Message).Returns(command);

        _coursesModuleMock
            .Setup(x => x.Send(It.IsAny<DeactivateEnrollmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Unauthorized("Tenant.NotFound", "Tenant not found."));

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(
            It.Is<StudentEnrollmentRevocationFailedEvent>(e => e.CorrelationId == command.CorrelationId),
            It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.Publish(
            It.IsAny<StudentEnrollmentRevokedFromSagaEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_SendDeactivationCommand_WithMessagePayload()
    {
        // Arrange
        var command = new RevokeStudentEnrollmentFromSagaCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _contextMock.Setup(x => x.Message).Returns(command);
        _coursesModuleMock
            .Setup(x => x.Send(It.IsAny<DeactivateEnrollmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(
            It.Is<DeactivateEnrollmentCommand>(cmd =>
                cmd.UserId == command.UserId && cmd.CourseId == command.CourseId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
