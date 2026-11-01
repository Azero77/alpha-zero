using System.Collections;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Reactivate;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class ReactivateEnrollmentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IEnrollmentRepository _repository = Substitute.For<IEnrollmentRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly ReactivateEnrollmentCommandHandler _handler;

    public ReactivateEnrollmentCommandHandlerTests()
    {
        var transitionService = new EnrollmentStatusTransitionService(_publisher, _repository, Substitute.For<ILogger<EnrollmentStatusTransitionService>>());
        _handler = new ReactivateEnrollmentCommandHandler(_repository, Substitute.For<IAuthorizationContextProvider>(), transitionService, Substitute.For<ILogger<ReactivateEnrollmentCommandHandler>>());
    }

    private static EnrollmentAggregate CreateEnrollment()
    {
        return EnrollmentAggregate.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), Guid.NewGuid()).Value;
    }

    private void SetupExisting(EnrollmentAggregate enrollment)
    {
        _repository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Inactive)]
    [InlineData(EnrollmentStatus.Suspended)]
    [InlineData(EnrollmentStatus.Expired)]
    public async Task Handle_Should_ReactivateNonActiveEnrollment_AndPersist(EnrollmentStatus startingStatus)
    {
        // Arrange
        var enrollment = CreateEnrollment();
        enrollment.CompleteItem(1, new BitArray(3, true)).IsError.Should().BeFalse();
        SetupExisting(enrollment);
        switch (startingStatus)
        {
            case EnrollmentStatus.Inactive:
                enrollment.Deactivate().IsError.Should().BeFalse();
                break;
            case EnrollmentStatus.Suspended:
                enrollment.Suspend().IsError.Should().BeFalse();
                break;
            case EnrollmentStatus.Expired:
                enrollment.MarkExpired().IsError.Should().BeFalse();
                break;
        }

        // Act
        var result = await _handler.Handle(new ReactivateEnrollmentCommand(enrollment.Id), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.Progress.IsComplete(1).Should().BeTrue(); // story #2: progress intact
        _repository.Received(1).Update(enrollment);
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenAlreadyActive()
    {
        // Arrange
        var enrollment = CreateEnrollment();
        SetupExisting(enrollment);

        // Act
        var result = await _handler.Handle(new ReactivateEnrollmentCommand(enrollment.Id), CancellationToken.None);

        // Assert: success, and a second write is not queued (story #11)
        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        _repository.DidNotReceive().Update(Arg.Any<EnrollmentAggregate>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEnrollmentDoesNotExist()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EnrollmentAggregate?)null);

        // Act
        var result = await _handler.Handle(new ReactivateEnrollmentCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.FirstError.Code.Should().Be("Enrollment.NotFound");
    }
}
