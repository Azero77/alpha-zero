using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseStateHandlersTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly CoursePublishingService _publishingService;
    private readonly ILogger<CourseStateHandlers> _logger;
    private readonly CourseStateHandlers _handler;

    public CourseStateHandlersTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _courseOverviewRepository = Substitute.For<ICourseOverviewRepository>();
        _publishingService = new CoursePublishingService(new CourseReadinessService());
        _logger = Substitute.For<ILogger<CourseStateHandlers>>();
        _handler = new CourseStateHandlers(_courseRepository, _courseOverviewRepository, _publishingService, _logger);
    }

    private void SetupQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSections().Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    [Fact]
    public async Task SubmitForReview_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        SetupQuery(courseId, null);
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
        
        SetupQuery(course.Id, course);
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
        var section = course.Sections.First();
        var v1Id = Guid.NewGuid();
        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, v1Id, 120);
        section.AddItem(Guid.NewGuid(), "L1", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.SubmitForReview(); // Now UnderReview

        SetupQuery(course.Id, course);
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
        var section = course.Sections.First();
        var v2Id = Guid.NewGuid();
        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, v2Id, 120);
        section.AddItem(Guid.NewGuid(), "L1", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.SubmitForReview();

        SetupQuery(course.Id, course);
        var command = new RejectCourseCommand(course.Id, "Incomplete content");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public async Task Publish_Should_FailWithConflict_WhenDraftCourseAndCallerCannotDirectPublish()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        // Status is Draft

        SetupQuery(course.Id, course);
        var command = new PublishCourseCommand(course.Id, CanDirectPublish: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
        course.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public async Task Publish_Should_Fail_WhenDraftCourseAndCallerCanDirectPublish_ButReadinessFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        // Status is Draft, has no sections or plans

        SetupQuery(course.Id, course);
        var command = new PublishCourseCommand(course.Id, CanDirectPublish: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        // Since no plans, validation fails first:
        result.FirstError.Code.Should().Be("Course.NoPlans");
    }

    [Fact]
    public async Task Publish_Should_FailWithCourseNotReady_WhenDraftCourseHasPlan_ButReadinessCheckFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddPlan("Standard", Guid.NewGuid());
        // Status is Draft, has plan, but no sections or cover image

        SetupQuery(course.Id, course);
        var command = new PublishCourseCommand(course.Id, CanDirectPublish: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotReady");
    }

    [Fact]
    public async Task Publish_Should_Succeed_WhenDraftCourseAndCanDirectPublish_AndAllReadinessPasses()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("S1");
        var section = course.Sections.First();

        var assetId = Guid.NewGuid();
        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, assetId, 120);
        section.AddItem(Guid.NewGuid(), "Item 1", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.AddPlan("Standard", Guid.NewGuid());

        var overview = CourseOverview.Create(
            Guid.NewGuid(),
            tenantId,
            course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null,
            null,
            Guid.NewGuid()); // has cover image

        SetupQuery(course.Id, course);
        _courseOverviewRepository.GetFirst(
            Arg.Any<System.Linq.Expressions.Expression<Func<CourseOverview, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(overview);

        var command = new PublishCourseCommand(course.Id, CanDirectPublish: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
        _courseRepository.Received(1).Update(course);
    }
}
