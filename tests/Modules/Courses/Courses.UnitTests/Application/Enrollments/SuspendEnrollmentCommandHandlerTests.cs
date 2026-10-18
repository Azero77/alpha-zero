using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Suspend;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class SuspendEnrollmentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IEnrollmentRepository _repository = Substitute.For<IEnrollmentRepository>();

    private static EnrollmentAggregate CreateEnrollment()
    {
        return EnrollmentAggregate.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), Guid.NewGuid(), 3).Value;
    }

    [Fact]
    public async Task Handle_Should_SuspendActiveEnrollment_AndPersist()
    {
        // Arrange
        var enrollment = CreateEnrollment();
        _repository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);

        var repository = _repository;
        var handler = new SuspendEnrollmentCommandHandler(repository, Substitute.For<IAuthorizationContextProvider>(), Substitute.For<ILogger<SuspendEnrollmentCommandHandler>>());

        // Act
        var result = await handler.Handle(new SuspendEnrollmentCommand(enrollment.Id, Reason: "billing dispute"), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Suspended);
        _repository.Received(1).Update(enrollment);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEnrollmentDoesNotExist()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EnrollmentAggregate?)null);
        var handler = new SuspendEnrollmentCommandHandler(_repository, Substitute.For<IAuthorizationContextProvider>(), Substitute.For<ILogger<SuspendEnrollmentCommandHandler>>());

        // Act
        var result = await handler.Handle(new SuspendEnrollmentCommand(Guid.NewGuid(), null), CancellationToken.None);

        // Assert
        result.FirstError.Code.Should().Be("Enrollment.NotFound");
    }
}
