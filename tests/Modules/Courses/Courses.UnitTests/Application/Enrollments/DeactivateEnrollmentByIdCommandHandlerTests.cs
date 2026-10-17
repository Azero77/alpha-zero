using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class DeactivateEnrollmentByIdCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IEnrollmentRepository _repository = Substitute.For<IEnrollmentRepository>();
    private readonly ILogger<DeactivateEnrollmentByIdCommandHandler> _logger = Substitute.For<ILogger<DeactivateEnrollmentByIdCommandHandler>>();
    private readonly DeactivateEnrollmentByIdCommandHandler _handler;

    public DeactivateEnrollmentByIdCommandHandlerTests()
    {
        _handler = new DeactivateEnrollmentByIdCommandHandler(_repository, _logger);
    }

    private static EnrollmentAggregate CreateEnrollment()
    {
        return EnrollmentAggregate.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), Guid.NewGuid(), 3).Value;
    }

    [Fact]
    public async Task Handle_Should_DeactivateActiveEnrollment_AndPersist()
    {
        // Arrange
        var enrollment = CreateEnrollment();
        _repository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);

        // Act
        var result = await _handler.Handle(new DeactivateEnrollmentByIdCommand(enrollment.Id), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Inactive);
        _repository.Received(1).Update(enrollment);
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenAlreadyInactive()
    {
        // Arrange
        var enrollment = CreateEnrollment();
        enrollment.Deactivate();
        _repository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);

        // Act
        var result = await _handler.Handle(new DeactivateEnrollmentByIdCommand(enrollment.Id), CancellationToken.None);

        // Assert: success, and a second write is not queued
        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Inactive);
        _repository.DidNotReceive().Update(Arg.Any<EnrollmentAggregate>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEnrollmentDoesNotExist()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EnrollmentAggregate?)null);

        // Act
        var result = await _handler.Handle(new DeactivateEnrollmentByIdCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.NotFound");
    }

    [Fact]
    public async Task Handle_Should_RetainProgressBits_WhenDeactivating()
    {
        // Arrange: a student who had completed an item keeps that earned slot
        var enrollment = CreateEnrollment();
        enrollment.CompleteItem(1);
        _repository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);

        // Act
        await _handler.Handle(new DeactivateEnrollmentByIdCommand(enrollment.Id), CancellationToken.None);

        // Assert
        enrollment.Status.Should().Be(EnrollmentStatus.Inactive);
        enrollment.Progress.IsComplete(1).Should().BeTrue();
        enrollment.Progress.CompletionPercentage.Should().BeApproximately(33.33, 0.01);
    }
}
