using AlphaZero.Modules.Courses.Application.Courses.Commands.Sections;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using EnrollmentAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments.Enrollment;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseSectionLifecycleHandlersTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly IEnrollmentRepository _enrollmentRepository = Substitute.For<IEnrollmentRepository>();
    private readonly ILogger<CourseSectionHandlers> _logger = Substitute.For<ILogger<CourseSectionHandlers>>();
    private readonly CourseSectionHandlers _handler;

    public CourseSectionLifecycleHandlersTests()
    {
        _handler = new CourseSectionHandlers(_courseRepository, _enrollmentRepository, _logger);
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
        s1.AddItem(Guid.NewGuid(), "L1", "Video", course.AllocateNextBitIndex());
        s1.AddItem(Guid.NewGuid(), "L2", "Video", course.AllocateNextBitIndex());
        s2.AddItem(Guid.NewGuid(), "L3", "Video", course.AllocateNextBitIndex());
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
    public async Task Delete_Should_RefreshEnrollmentsActiveItems_AfterSectionDelete()
    {
        // Arrange
        var course = CreateCourseWithContent();
        var sectionId = course.Sections.ElementAt(0).Id;
        SetupCourseQuery(course.Id, course);

        var enrollment = Enrollment.Create(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id,
            course.TotalTrackedItems, course.ActiveTrackedItems).Value;
        _enrollmentRepository.GetCourseEnrollmentsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnrollmentAggregate> { enrollment });

        // Act: delete the section holding 2 of the 3 items
        var result = await _handler.Handle(new DeleteCourseSectionCommand(course.Id, sectionId), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.ActiveTrackedItems.Should().Be(1);
        enrollment.Progress.ActiveItems.Should().Be(1);
        _enrollmentRepository.Received(1).Update(enrollment);
        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Delete_Should_NotChangeEnrollmentBitmask_WhenRefreshing()
    {
        // Arrange: student completed bit index 2 (in the surviving section)
        var course = CreateCourseWithContent();
        var sectionId = course.Sections.ElementAt(0).Id;
        SetupCourseQuery(course.Id, course);

        var enrollment = Enrollment.Create(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id,
            course.TotalTrackedItems, course.ActiveTrackedItems).Value;
        enrollment.CompleteItem(2);
        var bitsBefore = new[] { enrollment.Progress.IsComplete(2) };

        _enrollmentRepository.GetCourseEnrollmentsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnrollmentAggregate> { enrollment });

        // Act
        await _handler.Handle(new DeleteCourseSectionCommand(course.Id, sectionId), CancellationToken.None);

        // Assert: earned bits survive the instructor edit
        enrollment.Progress.IsComplete(2).Should().Be(bitsBefore[0]);
        enrollment.Progress.CompletionPercentage.Should().Be(100.0);
    }
}
