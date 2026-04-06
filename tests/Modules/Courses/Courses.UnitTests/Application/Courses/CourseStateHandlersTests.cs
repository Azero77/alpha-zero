using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseStateHandlersTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CourseStateHandlers> _logger;
    private readonly CourseStateHandlers _handler;

    public CourseStateHandlersTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _logger = Substitute.For<ILogger<CourseStateHandlers>>();
        _handler = new CourseStateHandlers(_courseRepository, _logger);
    }

    [Fact]
    public async Task SubmitForReview_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseRepository.GetByIdWithSectionsAsync(courseId, Arg.Any<CancellationToken>()).Returns((Course?)null);
        var command = new SubmitCourseForReviewCommand(courseId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Approve_Should_Fail_WhenCourseIsNotUnderReview()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", subjectId).Value;
        // Status is Draft
        
        _courseRepository.GetByIdWithSectionsAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new ApproveCourseCommand(course.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
        course.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public async Task Reject_Should_RequireReason()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", subjectId).Value;
        course.AddSection("S1");
        course.AddLesson(course.Sections.First().Id, "L1", Guid.NewGuid());
        course.SubmitForReview(); // Now UnderReview

        _courseRepository.GetByIdWithSectionsAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new RejectCourseCommand(course.Id, ""); // Empty reason

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.RejectionReason");
    }

    [Fact]
    public async Task Reject_Should_MoveStatusBackToDraft()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", subjectId).Value;
        course.AddSection("S1");
        course.AddLesson(course.Sections.First().Id, "L1", Guid.NewGuid());
        course.SubmitForReview();

        _courseRepository.GetByIdWithSectionsAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new RejectCourseCommand(course.Id, "Incomplete content");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Draft);
    }
}
