using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using FluentAssertions;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CourseTests : DomainTest
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    [Fact]
    public void Create_Should_SetStatusToDraft()
    {
        // Act
        var result = Course.Create(Guid.NewGuid(), TenantId, "Physics 101", "Basics", SubjectId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public void SubmitForReview_Should_Fail_WhenCourseIsEmpty()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Empty Course", null, SubjectId).Value;

        // Act
        var result = course.SubmitForReview();

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Empty");
    }

    [Fact]
    public void Publish_Should_RaiseDomainEvent_WhenApproved()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Title", null, SubjectId).Value;
        course.AddSection("Section 1");
        course.AddLesson(course.Sections.First().Id, "Lesson 1", Guid.NewGuid());
        course.SubmitForReview();
        course.Approve();

        // Act
        var result = course.Publish();

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
        AssertDomainEvent<CoursePublishedDomainEvent>(course);
    }

    [Fact]
    public void ReorderSections_Should_Fail_WhenPublished()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Title", null, SubjectId).Value;
        course.AddSection("S1");
        course.AddSection("S2");
        course.AddLesson(course.Sections.First().Id, "L1", Guid.NewGuid());
        course.SubmitForReview();
        course.Approve();
        course.Publish();

        // Act
        var result = course.ReorderSections(new List<Guid> { Guid.NewGuid() });

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
    }
}
