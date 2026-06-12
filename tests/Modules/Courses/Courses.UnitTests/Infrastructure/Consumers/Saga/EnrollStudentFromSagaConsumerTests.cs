using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers.Saga;

public class EnrollStudentFromSagaConsumerTests
{
    private readonly Mock<ICoursesModule> _coursesModuleMock;
    private readonly Mock<ICourseRepository> _courseRepositoryMock;
    private readonly Mock<ILogger<EnrollStudentFromSagaConsumer>> _loggerMock;
    private readonly Mock<ConsumeContext<EnrollStudentFromSagaCommand>> _contextMock;
    private readonly EnrollStudentFromSagaConsumer _consumer;

    public EnrollStudentFromSagaConsumerTests()
    {
        _coursesModuleMock = new Mock<ICoursesModule>();
        _courseRepositoryMock = new Mock<ICourseRepository>();
        _loggerMock = new Mock<ILogger<EnrollStudentFromSagaConsumer>>();
        _contextMock = new Mock<ConsumeContext<EnrollStudentFromSagaCommand>>();
        
        _consumer = new EnrollStudentFromSagaConsumer(
            _coursesModuleMock.Object, 
            _courseRepositoryMock.Object, 
            _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_Should_PublishFailedEvent_WhenCourseNotFound()
    {
        // Arrange
        var command = new EnrollStudentFromSagaCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Standard", ResourceArn.ForCourse(Guid.NewGuid(), Guid.NewGuid()));
        _contextMock.Setup(x => x.Message).Returns(command);
        _courseRepositoryMock.Setup(x => x.GetCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course?)null);

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(It.Is<StudentEnrollmentFailedEvent>(e => e.CorrelationId == command.CorrelationId && e.Reason == "Course not found."), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_PublishFailedEvent_WhenPlanNotFound()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var command = new EnrollStudentFromSagaCommand(Guid.NewGuid(), Guid.NewGuid(), courseId, "Premium", ResourceArn.ForCourse(Guid.NewGuid(), courseId));
        _contextMock.Setup(x => x.Message).Returns(command);
        
        var course = Course.Create(courseId, Guid.NewGuid(), "Title", null, Guid.NewGuid()).Value;
        course.AddPlan("Standard", Guid.NewGuid()); // Add different plan

        _courseRepositoryMock.Setup(x => x.GetCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(It.Is<StudentEnrollmentFailedEvent>(e => e.CorrelationId == command.CorrelationId && e.Reason.Contains("Plan 'Premium' not found on Course")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_PublishEnrolledEvent_WhenSuccessful()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var command = new EnrollStudentFromSagaCommand(Guid.NewGuid(), Guid.NewGuid(), courseId, "Standard", ResourceArn.ForCourse(Guid.NewGuid(), courseId));
        _contextMock.Setup(x => x.Message).Returns(command);
        
        var course = Course.Create(courseId, Guid.NewGuid(), "Title", null, Guid.NewGuid()).Value;
        course.AddPlan("Standard", principalId);

        _courseRepositoryMock.Setup(x => x.GetCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(course);
            
        _coursesModuleMock.Setup(x => x.Send(It.IsAny<EnrollInCourseCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _consumer.Consume(_contextMock.Object);

        // Assert
        _contextMock.Verify(x => x.Publish(It.Is<StudentEnrolledFromSagaEvent>(e => e.CorrelationId == command.CorrelationId && e.PrincipalId == principalId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
