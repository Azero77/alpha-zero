using AlphaZero.Modules.Courses.Application.Enrollements.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollements;

public class CompleteItemCommandHandlerTests
{
    private readonly IEnrollementRepository _enrollementRepository;
    private readonly ILogger<CompleteItemCommandHandler> _logger;
    private readonly CompleteItemCommandHandler _handler;

    public CompleteItemCommandHandlerTests()
    {
        _enrollementRepository = Substitute.For<IEnrollementRepository>();
        _logger = Substitute.For<ILogger<CompleteItemCommandHandler>>();
        _handler = new CompleteItemCommandHandler(_enrollementRepository, _logger);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenEnrollmentIsSuspended()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollment = Enrollement.Create(Guid.NewGuid(), tenantId, studentId, courseId, 5).Value;
        
        // Use a private setter or a method if available, currently Enrollement has internal/public access?
        // Looking at Enrollement.cs, Status setter is private. I need a way to change it for testing.
        // Let's check if there's a Deactivate or similar method.
        enrollment.Deactivate(); // Status = Inactive

        _enrollementRepository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
        var command = new CompleteItemCommand(enrollment.Id, 0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollement.Status");
    }

    [Fact]
    public async Task Handle_Should_Success_WhenEnrollmentIsActive()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollment = Enrollement.Create(Guid.NewGuid(), tenantId, studentId, courseId, 5).Value;
        
        _enrollementRepository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
        var command = new CompleteItemCommand(enrollment.Id, 0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        enrollment.Progress.IsComplete(0).Should().BeTrue();
    }
}
