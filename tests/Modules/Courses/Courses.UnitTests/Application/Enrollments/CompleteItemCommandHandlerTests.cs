using AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class CompleteItemCommandHandlerTests
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ILogger<CompleteItemCommandHandler> _logger;
    private readonly CompleteItemCommandHandler _handler;

    public CompleteItemCommandHandlerTests()
    {
        _enrollmentRepository = Substitute.For<IEnrollmentRepository>();
        _logger = Substitute.For<ILogger<CompleteItemCommandHandler>>();
        _handler = new CompleteItemCommandHandler(_enrollmentRepository, _logger);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenEnrollmentIsSuspended()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollment = Enrollment.Create(Guid.NewGuid(), tenantId, studentId, courseId, 5).Value;
        
        // Use a private setter or a method if available, currently Enrollment has internal/public access?
        // Looking at Enrollment.cs, Status setter is private. I need a way to change it for testing.
        // Let's check if there's a Deactivate or similar method.
        enrollment.Deactivate(); // Status = Inactive

        _enrollmentRepository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
        var command = new CompleteItemCommand(enrollment.Id, 0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.Status");
    }

    [Fact]
    public async Task Handle_Should_Success_WhenEnrollmentIsActive()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollment = Enrollment.Create(Guid.NewGuid(), tenantId, studentId, courseId, 5).Value;
        
        _enrollmentRepository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
        var command = new CompleteItemCommand(enrollment.Id, 0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        enrollment.Progress.IsComplete(0).Should().BeTrue();
    }
}
