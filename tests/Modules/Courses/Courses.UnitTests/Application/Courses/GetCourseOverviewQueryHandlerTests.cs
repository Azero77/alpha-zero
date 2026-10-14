using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class GetCourseOverviewQueryHandlerTests
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly GetCourseOverviewQueryHandler _handler;

    public GetCourseOverviewQueryHandlerTests()
    {
        _courseQueryService = Substitute.For<ICourseQueryService>();
        _handler = new GetCourseOverviewQueryHandler(_courseQueryService);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenOverviewDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseQueryService.GetCourseOverviewAsync(courseId, Arg.Any<CancellationToken>())
            .Returns((CourseOverviewDto?)null);

        var query = new GetCourseOverviewQuery(courseId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseOverview.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Should_ReturnOverview_WhenFound()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var dto = new CourseOverviewDto(
            Guid.NewGuid(),
            courseId,
            "Published",
            "Course description",
            "Developers",
            "Learn testing",
            Guid.NewGuid(),
            "\"etag123\"");

        _courseQueryService.GetCourseOverviewAsync(courseId, Arg.Any<CancellationToken>())
            .Returns(dto);

        var query = new GetCourseOverviewQuery(courseId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEquivalentTo(dto);
    }
}
