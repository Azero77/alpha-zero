using AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

/// <summary>
/// Step 8 (issue #34 rework §3.4 path a): the guarded lazy mark. The duplicate-event case is the
/// storm-safety core — the SECOND PrincipalExpired evaluation while marking is pending must not
/// write anything (repo mark, domain events, audit) while still succeeding.
/// </summary>
public class MarkEnrollmentExpiredFromPrincipalCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IEnrollmentRepository _repository = Substitute.For<IEnrollmentRepository>();
    private readonly MarkEnrollmentExpiredFromPrincipalCommandHandler _handler;

    public MarkEnrollmentExpiredFromPrincipalCommandHandlerTests()
    {
        _handler = new MarkEnrollmentExpiredFromPrincipalCommandHandler(
            _repository, Substitute.For<ILogger<MarkEnrollmentExpiredFromPrincipalCommandHandler>>());
    }

    private static EnrollmentAggregate CreateActiveEnrollment()
    {
        var enrollment = EnrollmentAggregate.Create(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), Guid.NewGuid()).Value;
        enrollment.CompleteItem(0, new System.Collections.BitArray(2, true)).IsError.Should().BeFalse();
        enrollment.PopDomainEvents();
        return enrollment;
    }

    [Fact]
    public async Task Handle_Should_MarkActiveEnrollmentExpired_AndPersist()
    {
        var enrollment = CreateActiveEnrollment();
        _repository.GetByStudentAndCourseAsync(enrollment.StudentId, enrollment.CourseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(
            new MarkEnrollmentExpiredFromPrincipalCommand(enrollment.StudentId, enrollment.CourseId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Expired);
        _repository.Received(1).Update(enrollment);
        // the round-bound audit + integration-event flow rides the existing DomainEventHandler
        enrollment.DomainEvents.OfType<EnrollmentExpiredDomainEvent>().Should().ContainSingle()
            .Which.FromStatus.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task Handle_Should_NoOp_WhenAlreadyExpired_DuplicateMarkRunsOnce()
    {
        var enrollment = CreateActiveEnrollment();
        enrollment.MarkExpired().IsError.Should().BeFalse(); // first evaluation already marked it
        enrollment.PopDomainEvents();
        _repository.GetByStudentAndCourseAsync(enrollment.StudentId, enrollment.CourseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(
            new MarkEnrollmentExpiredFromPrincipalCommand(enrollment.StudentId, enrollment.CourseId), CancellationToken.None);

        result.IsError.Should().BeFalse(); // cheap success — the storm-safety contract
        enrollment.Status.Should().Be(EnrollmentStatus.Expired);
        _repository.DidNotReceive().Update(Arg.Any<EnrollmentAggregate>()); // no-op writes nothing
        enrollment.DomainEvents.Should().BeEmpty(); // no duplicate audit/event round
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEnrollmentIsNotVisibleInThisTenant()
    {
        _repository.GetByStudentAndCourseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((EnrollmentAggregate?)null);

        var result = await _handler.Handle(
            new MarkEnrollmentExpiredFromPrincipalCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidTransition_WhenEnrollmentIsTerminallyInactive()
    {
        var enrollment = CreateActiveEnrollment();
        enrollment.Deactivate().IsError.Should().BeFalse();
        enrollment.PopDomainEvents();
        _repository.GetByStudentAndCourseAsync(enrollment.StudentId, enrollment.CourseId, Arg.Any<CancellationToken>())
            .Returns(enrollment);

        var result = await _handler.Handle(
            new MarkEnrollmentExpiredFromPrincipalCommand(enrollment.StudentId, enrollment.CourseId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.InvalidTransition");
        _repository.DidNotReceive().Update(Arg.Any<EnrollmentAggregate>());
        enrollment.DomainEvents.Should().BeEmpty();
    }
}
