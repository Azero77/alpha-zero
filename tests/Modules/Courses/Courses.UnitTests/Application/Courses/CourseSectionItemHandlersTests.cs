using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
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

    private void SetupQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSection(Arg.Any<Guid>()).Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    [Fact]
    public async Task Create_Should_AddItem_WhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var assetId = Guid.NewGuid();

        SetupQuery(course.Id, course);

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

        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        SetupQuery(courseId, null);

        var content = JsonDocument.Parse("[]").RootElement;
        var command = new CreateCourseSectionItemCommand(courseId, sectionId, "Item", "Video", content);

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
        var missingSectionId = Guid.NewGuid();
        SetupQuery(course.Id, course);

        var content = JsonDocument.Parse("[]").RootElement;
        var command = new CreateCourseSectionItemCommand(course.Id, missingSectionId, "Item", "Article", content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Section");
    }

    [Fact]
    public async Task Update_Should_UpdateItemAndPreserveBitIndex_WhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var section = course.Sections.First();
        var sectionId = section.Id;

        var asset1Id = Guid.NewGuid();
        var asset2Id = Guid.NewGuid();

        var res1 = new VideoCurriculumResource(Guid.NewGuid(), 0, asset1Id, 100);
        var item = section.AddItem(Guid.NewGuid(), "Lesson 1", "Video", course.AllocateNextBitIndex(), new[] { res1 }).Value;

        SetupQuery(course.Id, course);

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

        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Delete_Should_SoftDeleteItem()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var course = Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Section 1");
        var section = course.Sections.First();
        var sectionId = section.Id;

        var assetId = Guid.NewGuid();
        var res = new VideoCurriculumResource(Guid.NewGuid(), 0, assetId, 120);
        var item = section.AddItem(Guid.NewGuid(), "Lesson 1", "Video", course.AllocateNextBitIndex(), new[] { res }).Value;

        SetupQuery(course.Id, course);

        var command = new DeleteCourseSectionItemCommand(course.Id, sectionId, item.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();

        _courseRepository.Received(1).Update(course);
    }
}
