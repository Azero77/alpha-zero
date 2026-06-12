using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;

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
    public void Publish_Should_Fail_WhenNoPlans()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Title", null, SubjectId).Value;
        course.AddSection("Section 1");
        course.AddCurriculumItem(course.Sections.First().Id, "Lesson 1", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        course.SubmitForReview();
        course.Approve();

        // Act
        var result = course.Publish();

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NoPlans");
    }

    [Fact]
    public void Publish_Should_RaiseDomainEvent_WhenApprovedAndHasPlan()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Title", null, SubjectId).Value;
        course.AddSection("Section 1");
        course.AddCurriculumItem(course.Sections.First().Id, "Lesson 1", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        course.AddPlan("Standard", Guid.NewGuid());
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
        course.AddCurriculumItem(course.Sections.First().Id, "L1", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        course.AddPlan("Standard", Guid.NewGuid());
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
