using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseReadiness;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class GetCourseReadinessQueryHandlerTests
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly GetCourseReadinessQueryHandler _handler;

    public GetCourseReadinessQueryHandlerTests()
    {
        _courseQueryService = Substitute.For<ICourseQueryService>();
        _handler = new GetCourseReadinessQueryHandler(_courseQueryService);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseQueryService.GetCourseReadinessAsync(courseId, Arg.Any<CancellationToken>())
            .Returns((CourseReadiness?)null);

        var query = new GetCourseReadinessQuery(courseId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnReadinessEvaluation_WhenFound()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var readiness = new CourseReadiness(
            CanPublish: true,
            CanSubmitForReview: true,
            Checks: new CourseReadinessChecks(
                new OverviewReadinessChecks(true, true, true, true),
                new CurriculumReadinessChecks(true, true, true),
                new CommercialReadinessChecks(true)),
            BlockingReasons: new List<string>());

        _courseQueryService.GetCourseReadinessAsync(courseId, Arg.Any<CancellationToken>())
            .Returns(readiness);

        var query = new GetCourseReadinessQuery(courseId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.CanPublish.Should().BeTrue();
        result.Value.CanSubmitForReview.Should().BeTrue();
        result.Value.BlockingReasons.Should().BeEmpty();
    }
}
