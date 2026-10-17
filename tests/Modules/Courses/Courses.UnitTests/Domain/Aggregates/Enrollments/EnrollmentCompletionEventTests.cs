using System.Collections;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

// #32: CourseCompletedDomainEvent is a domain contract (its emission at the aggregate seam
// IS the contract), so these unit tests assert external behavior: the event, its identity
// payloads, its exactly-once transition semantics, and its interaction with progress
// recomputation after a section deletion.
public class EnrollmentCompletionEventTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    private Enrollment NewEnrollment(int totalItems = 4) =>
        Enrollment.Create(Guid.NewGuid(), TenantId, _studentId, _courseId, totalItems).Value;

    [Fact]
    public void CompleteItem_Should_RaiseCourseCompletedEvent_ExactlyWhenCourseFlipsToAllComplete()
    {
        var enrollment = NewEnrollment(totalItems: 2);

        // Before completion: not all complete → no completion event
        var first = enrollment.CompleteItem(0);
        first.IsError.Should().BeFalse();
        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().BeNull();

        // The flip: completing the last item raises the event exactly once
        var second = enrollment.CompleteItem(1);
        second.IsError.Should().BeFalse();

        var completed = enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().ToList();
        completed.Should().ContainSingle();
        completed.Single().EnrollmentId.Should().Be(enrollment.Id);
        completed.Single().CourseId.Should().Be(_courseId);
        completed.Single().StudentId.Should().Be(_studentId);
        completed.Single().TenantId.Should().Be(TenantId);
        completed.Single().CompletedOnUtc.Should().Be(enrollment.CompletedOn!.Value);
        enrollment.CompletedOn.Should().NotBeNull();
    }

    [Fact]
    public void CompleteItem_Should_NeverRaiseSecondCompletionEvent_OnReCompletedItems()
    {
        var enrollment = NewEnrollment(totalItems: 2);
        enrollment.CompleteItem(0);
        enrollment.CompleteItem(1); // flip

        var raisedEvents = enrollment.PopDomainEvents().OfType<CourseCompletedDomainEvent>().ToList();
        raisedEvents.Should().ContainSingle("setup: the course completed");
        var originalCompletedOn = enrollment.CompletedOn;

        // Re-completing the last item (and any already-complete item) must never re-raise
        var firstRetry = enrollment.CompleteItem(1);
        firstRetry.IsError.Should().BeFalse();
        var secondRetry = enrollment.CompleteItem(0);
        secondRetry.IsError.Should().BeFalse();

        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().Be(originalCompletedOn);
    }

    [Fact]
    public void CompleteItem_Should_NotRaiseCompletionEvent_WhenCourseStillIncomplete()
    {
        var enrollment = NewEnrollment(totalItems: 3);
        enrollment.CompleteItem(0);

        enrollment.DomainEvents.Should().Contain(e => e is ItemCompletedDomainEvent);
        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().BeNull();
    }

    // --- Section-deletion interaction (#32 spec; recompute semantics per spec #29:
    //     ActiveItems recompute, completion bitmask bits stay stable) ---

    [Fact]
    public void DeletingSection_Should_KeepIncompleteCourseIncomplete_UnderRecompute()
    {
        var enrollment = NewEnrollment(totalItems: 4);
        enrollment.CompleteItem(0);
        enrollment.CompleteItem(1);
        enrollment.PopDomainEvents(); // clear setup events

        // The instructor deletes the section that contained a COMPLETED item (bit 0);
        // spec #29 recomputes ActiveItems (4 → 3) while the completion bits stay stable.
        var recomputed = Progress
            .FromBitmask(new BitArray(enrollment.Progress.Bitmask), activeItems: 3);

        recomputed.IsAllComplete.Should().BeFalse(because: "a genuinely incomplete item (bit 3) remains active — deleting a section must not complete the course");

        // The aggregate raised nothing on its own and still refuses a spurious raise:
        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().BeNull();
    }

    [Fact]
    public void AlreadyCompleteCourse_Should_NotReraise_AfterSectionDeletionRecompute()
    {
        var enrollment = NewEnrollment(totalItems: 2);
        enrollment.CompleteItem(0);
        enrollment.CompleteItem(1); // flip → first (and only) raise
        var raised = enrollment.PopDomainEvents().OfType<CourseCompletedDomainEvent>().ToList();
        raised.Should().ContainSingle();

        // A section deletion recomputes ActiveItems; the course stays (re-)complete...
        var recomputed = Progress
            .FromBitmask(new BitArray(enrollment.Progress.Bitmask), activeItems: 1);
        recomputed.IsAllComplete.Should().BeTrue();

        // ...but a later re-completion attempt must never produce a second event.
        var retry = enrollment.CompleteItem(0);
        retry.IsError.Should().BeFalse();

        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
    }
}
