using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Modules.Courses.Application.Queries;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class GetCoursePoolQueryHandlerTests
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly GetCoursePoolQueryHandler _handler;

    public GetCoursePoolQueryHandlerTests()
    {
        _courseQueryService = Substitute.For<ICourseQueryService>();
        _handler = new GetCoursePoolQueryHandler(_courseQueryService);
    }

    [Fact]
    public async Task Handle_Should_DelegateToQueryService_AndReturnAssetList()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var expectedAssets = new List<CourseAssetDto>
        {
            new(
                Guid.NewGuid(),
                "Intro Video",
                "Video",
                "Available",
                "arn:test:video",
                DateTime.UtcNow,
                Duration: TimeSpan.FromMinutes(10),
                ThumbnailUrl: "https://example.com/thumb.jpg")
        };

        _courseQueryService.GetCoursePoolAsync(courseId, false, Arg.Any<CancellationToken>())
            .Returns(expectedAssets);

        var query = new GetCoursePoolQuery(courseId, IncludeAssigned: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEquivalentTo(expectedAssets);
        await _courseQueryService.Received(1).GetCoursePoolAsync(courseId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_PassIncludeAssignedFlag_ToQueryService()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var expectedAssets = new List<CourseAssetDto>();

        _courseQueryService.GetCoursePoolAsync(courseId, true, Arg.Any<CancellationToken>())
            .Returns(expectedAssets);

        var query = new GetCoursePoolQuery(courseId, IncludeAssigned: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
        await _courseQueryService.Received(1).GetCoursePoolAsync(courseId, true, Arg.Any<CancellationToken>());
    }
}
