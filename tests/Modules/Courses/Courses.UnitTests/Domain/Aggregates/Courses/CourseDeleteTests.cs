using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CourseDeleteTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static Course CreateCourseWithTwoSections(out CourseSection s1, out CourseSection s2)
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("Section 1");
        course.AddSection("Section 2");
        s1 = course.Sections.ElementAt(0);
        s2 = course.Sections.ElementAt(1);

        course.AddItem(s1.Id, "L1", ResourceType.Video);
        course.AddItem(s1.Id, "L2", ResourceType.Video);
        course.AddItem(s2.Id, "L3", ResourceType.Video);

        // Handler-equivalent event translation (the dispatcher doesn't run in unit tests):
        // every added item activates its allocated bit position.
        foreach (var item in course.Sections.SelectMany(x => x.Items).Where(i => !i.IsDeleted))
            course.ActivateCurriculumBit(item.BitIndex);

        return course;
    }

    [Fact]
    public void Delete_Should_MarkCourseAsSoftDeleted_Once()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Course", null, SubjectId).Value;

        // Act
        var result = course.Delete();

        // Assert
        result.IsError.Should().BeFalse();
        course.IsDeleted.Should().BeTrue();
        course.OnDeleted.Should().NotBeNull();

        // Act: second delete is refused
        var second = course.Delete();

        second.IsError.Should().BeTrue();
        second.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Restore_Should_UnmarkDeletedCourse()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Course", null, SubjectId).Value;
        course.Delete();

        // Act
        var result = course.Restore();

        // Assert
        result.IsError.Should().BeFalse();
        course.IsDeleted.Should().BeFalse();
        course.OnDeleted.Should().BeNull();

        // Act: restore of a live course is refused
        var secondRestore = course.Restore();

        secondRestore.IsError.Should().BeTrue();
    }

    [Fact]
    public void UpdateSectionTitle_Should_RenameSection_ThroughTheRoot()
    {
        // Arrange
        var course = CreateCourseWithTwoSections(out var s1, out _);
        var originalOrder = s1.Order;

        // Act
        var result = course.UpdateSectionTitle(s1.Id, "Renamed Section");

        // Assert
        result.IsError.Should().BeFalse();
        s1.Title.Should().Be("Renamed Section");
        s1.Order.Should().Be(originalOrder);
    }

    [Fact]
    public void UpdateSectionTitle_Should_Fail_WhenSectionDoesNotBelongToCourse()
    {
        // Arrange
        var course = CreateCourseWithTwoSections(out _, out _);

        // Act
        var result = course.UpdateSectionTitle(Guid.NewGuid(), "Renamed Section");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void DeleteSection_Should_SoftDeleteSection_ThroughTheRoot()
    {
        // Arrange
        var course = CreateCourseWithTwoSections(out var s1, out var s2);

        // Act
        var result = course.DeleteSection(s1.Id);

        // Assert
        result.IsError.Should().BeFalse();
        s1.IsDeleted.Should().BeTrue();
        s1.OnDeleted.Should().NotBeNull();

        // Sibling section stays active with its ordering intact
        s2.IsDeleted.Should().BeFalse();

        // Bit indexes remain stable across deletion
        s2.Items.ElementAt(0).BitIndex.Should().Be(2);

        // The deleted section's items are soft-deleted too (self-describing aggregate)
        s1.Items.Should().OnlyContain(i => i.IsDeleted);
        s1.Items.ElementAt(0).BitIndex.Should().Be(0);

        // Allocation state stays intact; the active mask is handler territory — the domain
        // captured it for the CourseSectionDeletedDomainEvent instead of mutating it here.
        course.TotalTrackedItems.Should().Be(3);
        course.DomainEvents.OfType<CourseSectionDeletedDomainEvent>().Single().BitIndexes.Should().BeEquivalentTo(new[] { 0, 1 });
    }

    [Fact]
    public void DeleteSection_Should_Fail_WhenSectionDoesNotBelongToCourse()
    {
        // Arrange
        var course = CreateCourseWithTwoSections(out _, out _);

        // Act
        var result = course.DeleteSection(Guid.NewGuid());

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void DeleteSection_EventHandlerTranslation_Should_DeactivateContainedBits_WithoutTouchingEnrollments()
    {
        // Arrange: student completed the last section's item (bit index 2)
        var course = CreateCourseWithTwoSections(out var s1, out _);
        var enrollment = Enrollment.Create(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id).Value;

        var courseMask = course.SnapshotActiveItemsBitmask();
        enrollment.CompleteItem(2, courseMask);
        var studentMaskLength = enrollment.Progress.Bitmask.Length;

        // Act: instructor deletes the first section (bits 0 and 1 deactivated via the event
        // payload — exactly what CourseActiveItemsDomainEventHandler applies in production)
        course.DeleteSection(s1.Id);
        course.DeactivateCurriculumBits(
            course.DomainEvents.OfType<CourseSectionDeletedDomainEvent>().Single().BitIndexes);

        // Assert: the course mask marks the deleted section positions inactive and keeps the
        // still-active position; the student's earned completion bits are untouched and the
        // percentage recomputes against the course's remaining active items.
        course.ActiveItemsBitmask.Length.Should().Be(3);
        course.ActiveItemsBitmask[0].Should().BeFalse();
        course.ActiveItemsBitmask[1].Should().BeFalse();
        course.ActiveItemsBitmask[2].Should().BeTrue();

        courseMask = course.SnapshotActiveItemsBitmask();
        enrollment.Progress.IsComplete(2).Should().BeTrue();
        enrollment.Progress.Bitmask.Length.Should().Be(studentMaskLength); // enrollment unchanged
        ProgressCalculation.CompletionPercentage(enrollment.Progress.Bitmask, courseMask).Should().Be(100.0);
        ProgressCalculation.IsAllComplete(enrollment.Progress.Bitmask, courseMask).Should().BeTrue();

        // Completing a deactivated position is refused without loading the curriculum
        enrollment.CompleteItem(0, courseMask).FirstError.Code.Should().Be("Progress.ItemInactive");
    }
}
