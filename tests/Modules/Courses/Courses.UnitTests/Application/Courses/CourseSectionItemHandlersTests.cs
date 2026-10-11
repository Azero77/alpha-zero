using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseSectionItemHandlersTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CourseSectionItemHandlers> _logger;
    private readonly CourseSectionItemHandlers _handler;

    public CourseSectionItemHandlersTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _logger = Substitute.For<ILogger<CourseSectionItemHandlers>>();
        _handler = new CourseSectionItemHandlers(_courseRepository, _logger);
    }

    [Fact]
    public async Task Create_Should_AddItemAndIncrementAssetRefCount_WhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var assetId = Guid.NewGuid();
        course.AddVideoAsset(assetId, ResourceArn.ForVideo(tenantId, assetId), "Kinematics 1");
        course.MarkAssetAvailable(assetId);

        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        var content = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{assetId}"" }}
        ]").RootElement;

        var command = new CreateCourseSectionItemCommand(course.Id, sectionId, "Lesson 1", "Video", content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.BitIndex.Should().Be(0);
        result.Value.MainType.Should().Be("Video");
        result.Value.Title.Should().Be("Lesson 1");

        var asset = course.Assets.First(a => a.Id == assetId);
        asset.RefCount.Should().Be(1);
        asset.State.Should().Be(CourseAssetState.InUse);

        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _courseRepository.GetByIdWithSectionsAndAssetsAsync(courseId, Arg.Any<CancellationToken>())
            .Returns((Course?)null);

        var content = JsonDocument.Parse("[]").RootElement;
        var command = new CreateCourseSectionItemCommand(courseId, Guid.NewGuid(), "Item", "Video", content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenSectionDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        var content = JsonDocument.Parse("[]").RootElement;
        var command = new CreateCourseSectionItemCommand(course.Id, Guid.NewGuid(), "Item", "Article", content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Section");
    }

    [Fact]
    public async Task Create_Should_ReturnConflict_WhenAssetIsPendingTranscoding()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var assetId = Guid.NewGuid();
        course.AddVideoAsset(assetId, ResourceArn.ForVideo(tenantId, assetId), "Pending Video"); // State is Pending

        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        var content = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{assetId}"" }}
        ]").RootElement;

        var command = new CreateCourseSectionItemCommand(course.Id, sectionId, "Lesson 1", "Video", content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public async Task Update_Should_ReconcileAssetsAndPreserveBitIndex_WhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var asset1Id = Guid.NewGuid();
        var asset2Id = Guid.NewGuid();
        course.AddVideoAsset(asset1Id, ResourceArn.ForVideo(tenantId, asset1Id), "Video 1");
        course.AddVideoAsset(asset2Id, ResourceArn.ForVideo(tenantId, asset2Id), "Video 2");
        course.MarkAssetAvailable(asset1Id);
        course.MarkAssetAvailable(asset2Id);

        var initialContent = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{asset1Id}"" }}
        ]").RootElement;

        var item = course.AddSectionItem(sectionId, "Lesson 1", "Video", initialContent).Value;

        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        // Update item to switch from asset1 to asset2
        var updatedContent = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{asset2Id}"" }}
        ]").RootElement;

        var command = new UpdateCourseSectionItemCommand(course.Id, sectionId, item.Id, "Updated Lesson 1", "Video", updatedContent);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.BitIndex.Should().Be(0); // BitIndex strictly immutable
        result.Value.Title.Should().Be("Updated Lesson 1");

        var asset1 = course.Assets.First(a => a.Id == asset1Id);
        asset1.RefCount.Should().Be(0);
        asset1.State.Should().Be(CourseAssetState.Available);

        var asset2 = course.Assets.First(a => a.Id == asset2Id);
        asset2.RefCount.Should().Be(1);
        asset2.State.Should().Be(CourseAssetState.InUse);

        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Delete_Should_SoftDeleteItemAndReleaseAssets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var assetId = Guid.NewGuid();
        course.AddVideoAsset(assetId, ResourceArn.ForVideo(tenantId, assetId), "Video 1");
        course.MarkAssetAvailable(assetId);

        var content = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{assetId}"" }}
        ]").RootElement;

        var item = course.AddSectionItem(sectionId, "Lesson 1", "Video", content).Value;

        _courseRepository.GetByIdWithSectionsAndAssetsAsync(course.Id, Arg.Any<CancellationToken>())
            .Returns(course);

        var command = new DeleteCourseSectionItemCommand(course.Id, sectionId, item.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();

        var asset = course.Assets.First(a => a.Id == assetId);
        asset.RefCount.Should().Be(0);
        asset.State.Should().Be(CourseAssetState.Available);

        _courseRepository.Received(1).Update(course);
    }
}
