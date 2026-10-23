using AlphaZero.Modules.Courses.Application.Courses.Commands.Sections;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseSectionLifecycleHandlersTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly ILogger<CourseSectionHandlers> _logger = Substitute.For<ILogger<CourseSectionHandlers>>();
    private readonly CourseSectionHandlers _handler;

    public CourseSectionLifecycleHandlersTests()
    {
        _handler = new CourseSectionHandlers(_courseRepository, _logger);
    }

    private void SetupCourseQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSections().Returns(builder);
        builder.WithSection(Arg.Any<Guid>()).Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    private static Course CreateCourseWithContent()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Course", null, SubjectId).Value;
        course.AddSection("S1");
        course.AddSection("S2");
        var s1 = course.Sections.ElementAt(0);
        var s2 = course.Sections.ElementAt(1);
        course.AddItem(s1.Id, "L1", ResourceType.Video);
        course.AddItem(s1.Id, "L2", ResourceType.Video);
        course.AddItem(s2.Id, "L3", ResourceType.Video);

        // Handler-equivalent event translation (no dispatcher in unit tests).
        foreach (var item in course.Sections.SelectMany(x => x.Items).Where(i => !i.IsDeleted))
            course.ActivateCurriculumBit(item.BitIndex);

        return course;
    }

    [Fact]
    public async Task Rename_Should_Fail_WhenSectionDoesNotBelongToCourse()
    {
        // Arrange
        var course = CreateCourseWithContent();
        SetupCourseQuery(course.Id, course);
        var command = new RenameCourseSectionCommand(course.Id, Guid.NewGuid(), "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Section");
    }

    [Fact]
    public async Task Rename_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        SetupCourseQuery(Guid.NewGuid(), null);
        var command = new RenameCourseSectionCommand(Guid.NewGuid(), Guid.NewGuid(), "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Rename_Should_Succeed_AndPersistCourse()
    {
        // Arrange
        var course = CreateCourseWithContent();
        var sectionId = course.Sections.ElementAt(0).Id;
        SetupCourseQuery(course.Id, course);
        var command = new RenameCourseSectionCommand(course.Id, sectionId, "Renamed Section");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Sections.ElementAt(0).Title.Should().Be("Renamed Section");
        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Delete_Should_ReturnNotFound_WhenSectionDoesNotBelongToCourse()
    {
        // Arrange
        var course = CreateCourseWithContent();
        SetupCourseQuery(course.Id, course);
        var command = new DeleteCourseSectionCommand(course.Id, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.FirstError.Code.Should().Be("Course.Section");
    }

    [Fact]
    public async Task Delete_Should_SoftDeleteSectionAndItsItems_WithoutTouchingEnrollments()
    {
        // Arrange
        var course = CreateCourseWithContent();
        var sectionId = course.Sections.ElementAt(0).Id;
        SetupCourseQuery(course.Id, course);

        // Act: delete the section holding 2 of the 3 items
        var result = await _handler.Handle(new DeleteCourseSectionCommand(course.Id, sectionId), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Sections.ElementAt(0).IsDeleted.Should().BeTrue();
        // The deleted section's items are soft-deleted, the sibling section's are not.
        course.Sections.ElementAt(0).Items.Should().OnlyContain(i => i.IsDeleted);
        course.Sections.ElementAt(1).Items.ElementAt(0).IsDeleted.Should().BeFalse();

        // The event payload carries the captured BitIndexes for the mask handler —
        // but the command itself performs NO enrollment reads or writes at all.
        course.DomainEvents.OfType<CourseSectionDeletedDomainEvent>().Single()
            .BitIndexes.Should().BeEquivalentTo(new[] { 0, 1 });
        _courseRepository.Received(1).Update(course);
    }
}
