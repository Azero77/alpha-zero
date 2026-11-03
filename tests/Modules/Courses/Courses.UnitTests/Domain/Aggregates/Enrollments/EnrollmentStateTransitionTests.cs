using System.Collections;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using ErrorOr;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

/// <summary>
/// Full transition-matrix coverage for the enrollment lifecycle — the executable form of the
/// matrix table pinned in plan comment on issue #34 (single source of truth).
/// </summary>
public class EnrollmentStateTransitionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Enrollment NewEnrollment()
    {
        var enrollment = Enrollment.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), Guid.NewGuid()).Value;
        enrollment.PopDomainEvents(); // drop the creation event
        return enrollment;
    }

    private static Enrollment SuspendedEnrollment()
    {
        var enrollment = NewEnrollment();
        enrollment.Suspend("reason").IsError.Should().BeFalse();
        enrollment.PopDomainEvents();
        return enrollment;
    }

    private static Enrollment InactiveEnrollment()
    {
        var enrollment = NewEnrollment();
        enrollment.Deactivate().IsError.Should().BeFalse();
        enrollment.PopDomainEvents();
        return enrollment;
    }

    private static Enrollment ExpiredEnrollment()
    {
        var enrollment = NewEnrollment();
        enrollment.MarkExpired().IsError.Should().BeFalse();
        enrollment.PopDomainEvents();
        return enrollment;
    }

    // ------------------------------------------------------------------
    // Legal transitions — status lands correctly and metadata is recorded
    // ------------------------------------------------------------------

    [Fact]
    public void Suspend_Should_MoveActiveToSuspended_AndEmitGuardedTransitionEvent()
    {
        var enrollment = NewEnrollment();

        var result = enrollment.Suspend("reason");

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Suspended);
    }

    [Fact]
    public void Deactivate_Should_MoveActiveToInactive_AndEmitAuditOnlyEvent()
    {
        var enrollment = NewEnrollment();

        var result = enrollment.Deactivate();

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Inactive);
    }

    [Fact]
    public void Reactivate_Should_MoveInactiveToActive_AndEmitResumptionEvent()
    {
        var enrollment = InactiveEnrollment();
        var operatorId = Guid.NewGuid();

        var result = enrollment.Reactivate(operatorId);

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Suspended)]
    [InlineData(EnrollmentStatus.Expired)]
    public void Reactivate_Should_MoveNonActiveStatesToActive(EnrollmentStatus startingStatus)
    {
        var enrollment = startingStatus == EnrollmentStatus.Suspended ? SuspendedEnrollment() : ExpiredEnrollment();

        var result = enrollment.Reactivate(Guid.NewGuid());

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public void MarkExpired_Should_MoveActiveToExpired_AndEmitExpiryEvent_WithSystemActor()
    {
        var enrollment = NewEnrollment();

        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = enrollment.MarkExpired();
        var after = DateTime.UtcNow.AddSeconds(1);

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Expired); // the aggregate owns the clock now
        var emitted = enrollment.DomainEvents.OfType<EnrollmentExpiredDomainEvent>().Should().ContainSingle().Subject;
        emitted.FromStatus.Should().Be(EnrollmentStatus.Active);
        emitted.OccuredOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void MarkExpired_Should_MoveSuspendedToExpired() // the clock does not respect a billing pause
    {
        var enrollment = SuspendedEnrollment();

        enrollment.MarkExpired().IsError.Should().BeFalse();

        enrollment.Status.Should().Be(EnrollmentStatus.Expired);
    }

    // ------------------------------------------------------------------
    // Idempotency — repeated trigger of the same transition succeeds with no rewrite
    // (no state churn, no new domain events) — stories #4, #11
    // ------------------------------------------------------------------

    [Fact]
    public void Suspend_Should_BeIdempotent_WhenAlreadySuspended()
    {
        var enrollment = SuspendedEnrollment();
        var previousEventCount = enrollment.DomainEvents.Count;

        var result = enrollment.Suspend("reason");

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Suspended);
        enrollment.DomainEvents.Count.Should().Be(previousEventCount);
    }

    [Fact]
    public void Deactivate_Should_BeIdempotent_WhenAlreadyInactive()
    {
        var enrollment = InactiveEnrollment();

        var result = enrollment.Deactivate();

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Inactive);
        enrollment.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reactivate_Should_BeIdempotent_WhenAlreadyActive()
    {
        var enrollment = NewEnrollment();
        var previousEventCount = enrollment.DomainEvents.Count;

        var result = enrollment.Reactivate(Guid.NewGuid());

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.DomainEvents.Count.Should().Be(previousEventCount);
    }

    [Fact]
    public void MarkExpired_Should_BeIdempotent_WhenAlreadyExpired()
    {
        var enrollment = ExpiredEnrollment();

        var result = enrollment.MarkExpired();

        result.IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Expired);
        enrollment.DomainEvents.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Illegal transitions — rejected with the domain conflict code, no state change
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(EnrollmentStatus.Inactive)]
    [InlineData(EnrollmentStatus.Expired)]
    public void Suspend_Should_RejectTransitionsFromNonActiveNonSuspendedStates(EnrollmentStatus startingStatus)
    {
        var enrollment = startingStatus == EnrollmentStatus.Inactive ? InactiveEnrollment() : ExpiredEnrollment();

        var result = enrollment.Suspend("reason");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.InvalidTransition");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        enrollment.Status.Should().Be(startingStatus);
        enrollment.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkExpired_Should_RejectTransitionFromInactive()
    {
        var enrollment = InactiveEnrollment(); // no access left to expire

        var result = enrollment.MarkExpired();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.InvalidTransition");
        enrollment.Status.Should().Be(EnrollmentStatus.Inactive);
    }

    // ------------------------------------------------------------------
    // Transitions never touch progress bits — users stories #1, #2
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(EnrollmentStatus.Suspended)]
    [InlineData(EnrollmentStatus.Inactive)]
    [InlineData(EnrollmentStatus.Expired)]
    public void Transitions_Should_PreserveProgressBitmask(EnrollmentStatus via)
    {
        var enrollment = NewEnrollment();
        enrollment.CompleteItem(2, new BitArray(5, true)).IsError.Should().BeFalse();

        switch (via)
        {
            case EnrollmentStatus.Suspended:
                enrollment.Suspend("reason").IsError.Should().BeFalse();
                break;
            case EnrollmentStatus.Inactive:
                enrollment.Deactivate().IsError.Should().BeFalse();
                break;
            case EnrollmentStatus.Expired:
                enrollment.MarkExpired().IsError.Should().BeFalse();
                break;
        }

        enrollment.Reactivate(Guid.NewGuid()).IsError.Should().BeFalse();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.Progress.IsComplete(2).Should().BeTrue();
        ProgressCalculation.CompletionPercentage(enrollment.Progress.Bitmask, new BitArray(5, true)).Should().BeApproximately(20.0, 0.01);
    }
}
