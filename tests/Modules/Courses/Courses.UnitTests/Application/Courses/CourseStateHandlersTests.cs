using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseStateHandlersTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly ILogger<CourseStateHandlers> _logger;
    private readonly CourseStateHandlers _handler;

    public CourseStateHandlersTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _courseOverviewRepository = Substitute.For<ICourseOverviewRepository>();
        _logger = Substitute.For<ILogger<CourseStateHandlers>>();
        _handler = new CourseStateHandlers(_courseRepository, _courseOverviewRepository, _logger);
    }

    [Fact]
    public async Task SubmitForReview_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseRepository.GetCourseAsync(courseId, Arg.Any<CancellationToken>()).Returns((Course?)null);
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
        
        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
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
        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(tenantId, v1Id), "L1");
        course.MarkAssetAvailable(v1Id);
        course.AssignAssetToCurriculum(v1Id, course.Sections.First().Id, "L1", JsonDocument.Parse("{}").RootElement);
        course.SubmitForReview(); // Now UnderReview

        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
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
        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(tenantId, v2Id), "L1");
        course.MarkAssetAvailable(v2Id);
        course.AssignAssetToCurriculum(v2Id, course.Sections.First().Id, "L1", JsonDocument.Parse("{}").RootElement);
        course.SubmitForReview();

        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new RejectCourseCommand(course.Id, "Incomplete content");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public async Task Publish_Should_FailWithConflict_WhenDraftCourseAndCallerIsNotTenantAdmin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        // Status is Draft

        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new PublishCourseCommand(course.Id, IsTenantAdmin: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
        course.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public async Task Publish_Should_FailWith422_WhenDraftCourseAndCallerIsTenantAdmin_ButReadinessFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        // Status is Draft, has no sections or plans

        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new PublishCourseCommand(course.Id, IsTenantAdmin: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        // Since no plans, validation fails first:
        result.FirstError.Code.Should().Be("Course.NoPlans");
    }

    [Fact]
    public async Task Publish_Should_FailWith422_WhenDraftCourseHasPlan_ButReadinessCheckFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddPlan("Standard", Guid.NewGuid());
        // Status is Draft, has plan, but no sections or cover image

        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        var command = new PublishCourseCommand(course.Id, IsTenantAdmin: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotReady");
        result.FirstError.NumericType.Should().Be(422);
    }

    [Fact]
    public async Task Publish_Should_Succeed_WhenDraftCourseAndCallerIsTenantAdmin_AndAllReadinessPasses()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("S1");
        var sectionId = course.Sections.First().Id;

        var assetId = Guid.NewGuid();
        course.AddVideoAsset(assetId, ResourceArn.ForVideo(tenantId, assetId), "Video 1");
        course.MarkAssetAvailable(assetId);

        var content = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{assetId}"" }}
        ]").RootElement;
        course.AddSectionItem(sectionId, "Item 1", "Video", content);
        course.AddPlan("Standard", Guid.NewGuid());

        var overview = CourseOverview.Create(
            Guid.NewGuid(),
            tenantId,
            course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null,
            null,
            Guid.NewGuid()); // has cover image

        _courseRepository.GetCourseAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        _courseOverviewRepository.GetFirst(
            Arg.Any<System.Linq.Expressions.Expression<Func<CourseOverview, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(overview);

        var command = new PublishCourseCommand(course.Id, IsTenantAdmin: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
        _courseRepository.Received(1).Update(course);
    }
}
