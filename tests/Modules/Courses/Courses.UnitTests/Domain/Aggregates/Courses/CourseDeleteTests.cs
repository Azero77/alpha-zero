using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
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

        s1.AddItem(Guid.NewGuid(), "L1", "Video", course.AllocateNextBitIndex());
        s1.AddItem(Guid.NewGuid(), "L2", "Video", course.AllocateNextBitIndex());
        s2.AddItem(Guid.NewGuid(), "L3", "Video", course.AllocateNextBitIndex());

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

        // Active tracked items recount against remaining active items
        course.TotalTrackedItems.Should().Be(3);
        course.ActiveTrackedItems.Should().Be(1);
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
    public void DeleteSection_Should_RecomputeEnrollmentProgress_WithoutLosingEarnedBits()
    {
        // Arrange: student completed the last section's item (bit index 2)
        var course = CreateCourseWithTwoSections(out var s1, out _);
        var enrollment = Enrollment.Create(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id,
            course.TotalTrackedItems, course.ActiveTrackedItems).Value;

        enrollment.CompleteItem(2);
        enrollment.Progress.CompletionPercentage.Should().BeApproximately(33.33, 0.01, because: "1 of 3 live items complete");

        // Act: instructor deletes the first section (2 of 3 items inactive)
        course.DeleteSection(s1.Id);
        enrollment.UpdateActiveItems(course.ActiveTrackedItems);

        // Assert: earned completion bit for the still-active item survives instructor edits,
        // and the percentage remarshals against the remaining active items
        enrollment.Progress.IsComplete(2).Should().BeTrue();
        enrollment.Progress.CompletionPercentage.Should().Be(100.0);
        enrollment.Progress.IsAllComplete.Should().BeTrue();
    }
}
