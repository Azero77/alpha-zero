using System.Collections;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Enrollments;

// #32: CourseCompletedDomainEvent is a domain contract (its emission at the aggregate seam
// IS the contract), so these unit tests assert external behavior: the event, its identity
// payloads, its exactly-once transition semantics, and its interaction with the course's
// active-items mask after curriculum deletions (#29).
public class EnrollmentCompletionEventTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    private Enrollment NewEnrollment() =>
        Enrollment.Create(Guid.NewGuid(), TenantId, _studentId, _courseId).Value;

    private static BitArray ActiveMask(int positions, params int[] deactivated)
    {
        var mask = new BitArray(positions, true);
        foreach (var index in deactivated) mask.Set(index, false);
        return mask;
    }

    [Fact]
    public void CompleteItem_Should_RaiseCourseCompletedEvent_ExactlyWhenCourseFlipsToAllComplete()
    {
        var enrollment = NewEnrollment();
        var activeMask = ActiveMask(2);

        // Before completion: not all complete → no completion event
        var first = enrollment.CompleteItem(0, activeMask);
        first.IsError.Should().BeFalse();
        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().BeNull();

        // The flip: completing the last active item raises the event exactly once
        var second = enrollment.CompleteItem(1, activeMask);
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
        var enrollment = NewEnrollment();
        var activeMask = ActiveMask(2);
        enrollment.CompleteItem(0, activeMask);
        enrollment.CompleteItem(1, activeMask); // flip

        var raisedEvents = enrollment.PopDomainEvents().OfType<CourseCompletedDomainEvent>().ToList();
        raisedEvents.Should().ContainSingle("setup: the course completed");
        var originalCompletedOn = enrollment.CompletedOn;

        // Re-completing the last item (and any already-complete item) must never re-raise
        var firstRetry = enrollment.CompleteItem(1, activeMask);
        firstRetry.IsError.Should().BeFalse();
        var secondRetry = enrollment.CompleteItem(0, activeMask);
        secondRetry.IsError.Should().BeFalse();

        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().Be(originalCompletedOn);
    }

    [Fact]
    public void CompleteItem_Should_NotRaiseCompletionEvent_WhenCourseStillIncomplete()
    {
        var enrollment = NewEnrollment();

        enrollment.CompleteItem(0, ActiveMask(3));

        enrollment.DomainEvents.Should().Contain(e => e is ItemCompletedDomainEvent);
        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().BeNull();
    }

    // --- Section-deletion interaction (#32 spec; active-mask semantics per spec #29) ---

    [Fact]
    public void DeletingSection_Should_KeepIncompleteCourseIncomplete()
    {
        var enrollment = NewEnrollment();
        var activeMask = ActiveMask(4);
        enrollment.CompleteItem(0, activeMask);
        enrollment.CompleteItem(1, activeMask);
        enrollment.PopDomainEvents(); // clear setup events

        // The instructor deletes the section containing COMPLETED bit 0: the course mask
        // deactivates that position, shrinking the denominator to the three remaining items.
        var afterDeletion = ActiveMask(4, deactivated: 0);

        ProgressCalculation.IsAllComplete(enrollment.Progress.Bitmask, afterDeletion)
            .Should().BeFalse(because: "two of the three remaining active items are incomplete");

        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().BeNull();

        // Completing the remaining active items still works against the recomputed mask.
        var next = enrollment.CompleteItem(2, afterDeletion);
        next.IsError.Should().BeFalse();
        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty(
            because: "one active item (bit 3) is still incomplete");
        enrollment.CompletedOn.Should().BeNull();
    }

    [Fact]
    public void AlreadyCompleteCourse_Should_NotReraise_AfterSectionDeletionRecompute()
    {
        var enrollment = NewEnrollment();
        var activeMask = ActiveMask(2);
        enrollment.CompleteItem(0, activeMask);
        enrollment.CompleteItem(1, activeMask); // flip → first (and only) raise
        var raised = enrollment.PopDomainEvents().OfType<CourseCompletedDomainEvent>().ToList();
        raised.Should().ContainSingle();
        var originalCompletedOn = enrollment.CompletedOn;

        // A section deletion deactivates a completed position; the course stays complete
        // because every remaining active item is complete.
        var afterDeletion = ActiveMask(2, deactivated: 0);
        ProgressCalculation.IsAllComplete(enrollment.Progress.Bitmask, afterDeletion)
            .Should().BeTrue();

        // ...but a later re-completion attempt must never produce a second event.
        var retry = enrollment.CompleteItem(1, afterDeletion);
        retry.IsError.Should().BeFalse();

        enrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty();
        enrollment.CompletedOn.Should().Be(originalCompletedOn);
    }
}
