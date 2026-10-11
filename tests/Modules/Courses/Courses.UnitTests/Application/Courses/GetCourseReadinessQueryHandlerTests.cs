using System.Linq.Expressions;
using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseReadiness;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.ValueObjects;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class GetCourseReadinessQueryHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly GetCourseReadinessQueryHandler _handler;

    public GetCourseReadinessQueryHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _courseOverviewRepository = Substitute.For<ICourseOverviewRepository>();
        _handler = new GetCourseReadinessQueryHandler(_courseRepository, _courseOverviewRepository);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseRepository.GetByIdWithSectionsAndAssetsAsync(courseId, Arg.Any<CancellationToken>())
            .Returns((Course?)null);

        var query = new GetCourseReadinessQuery(courseId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnReadinessEvaluation_WithBlockingReasons_WhenCourseIsUnready()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Thermodynamics", "Intro", Guid.NewGuid()).Value;

        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        _courseOverviewRepository.GetFirst(
            Arg.Any<Expression<Func<CourseOverview, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns((CourseOverview?)null);

        var query = new GetCourseReadinessQuery(course.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var readiness = result.Value;
        readiness.CanPublish.Should().BeFalse();
        readiness.Checks.Overview.HasTitle.Should().BeTrue();
        readiness.Checks.Curriculum.HasSections.Should().BeFalse();
        readiness.Checks.Curriculum.HasItems.Should().BeFalse();
        readiness.Checks.Commercial.HasActivePricingPlan.Should().BeFalse();
        readiness.BlockingReasons.Should().Contain("Course must have at least one section.");
    }

    [Fact]
    public async Task Handle_Should_ReturnCanPublishTrue_WhenAllCriteriaAreMet()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Thermodynamics", "Intro", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
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
            RichText.Create("{\"type\":\"doc\"}").Value,
            null,
            null,
            Guid.NewGuid()); // has cover image

        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        _courseOverviewRepository.GetFirst(
            Arg.Any<Expression<Func<CourseOverview, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(overview);

        var query = new GetCourseReadinessQuery(course.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.CanPublish.Should().BeTrue();
        result.Value.CanSubmitForReview.Should().BeTrue();
        result.Value.BlockingReasons.Should().BeEmpty();
    }
}
