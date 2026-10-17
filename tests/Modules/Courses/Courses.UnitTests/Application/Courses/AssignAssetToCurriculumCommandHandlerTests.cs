using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class AssignAssetToCurriculumCommandHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly AssignAssetToCurriculumCommandHandler _handler;

    public AssignAssetToCurriculumCommandHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _assetRepository = Substitute.For<IRepository<CourseAsset>>();
        _handler = new AssignAssetToCurriculumCommandHandler(_courseRepository, _assetRepository);
    }

    private void SetupQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSection(Arg.Any<Guid>()).Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAssetDoesNotExist()
    {
        // Arrange
        var command = new AssignAssetToCurriculumCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lesson 1");
        _assetRepository.GetById(command.AssetId, Arg.Any<CancellationToken>())
            .Returns((CourseAsset?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenAssetBelongsToDifferentCourse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId1 = Guid.NewGuid();
        var courseId2 = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, assetId);
        var asset = VideoCourseAsset.Create(assetId, tenantId, arn, "Video", courseId1).Value;

        var command = new AssignAssetToCurriculumCommand(courseId2, Guid.NewGuid(), assetId, "Lesson 1");
        _assetRepository.GetById(assetId, Arg.Any<CancellationToken>())
            .Returns(asset);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.WrongCourse");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenAssetIsNotAvailable()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, assetId);
        var asset = VideoCourseAsset.Create(assetId, tenantId, arn, "Video", courseId).Value;

        var command = new AssignAssetToCurriculumCommand(courseId, Guid.NewGuid(), assetId, "Lesson 1");
        _assetRepository.GetById(assetId, Arg.Any<CancellationToken>())
            .Returns(asset);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.NotReady");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, assetId);
        var asset = VideoCourseAsset.Create(assetId, tenantId, arn, "Video", courseId).Value;
        asset.MarkAvailable();

        var sectionId = Guid.NewGuid();
        var command = new AssignAssetToCurriculumCommand(courseId, sectionId, assetId, "Lesson 1");
        _assetRepository.GetById(assetId, Arg.Any<CancellationToken>())
            .Returns(asset);

        SetupQuery(courseId, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectionDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var course = Course.Create(courseId, tenantId, "Math 101", "Desc", Guid.NewGuid()).Value;

        var assetId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, assetId);
        var asset = VideoCourseAsset.Create(assetId, tenantId, arn, "Video", courseId).Value;
        asset.MarkAvailable();

        var missingSectionId = Guid.NewGuid();
        var command = new AssignAssetToCurriculumCommand(courseId, missingSectionId, assetId, "Lesson 1");
        _assetRepository.GetById(assetId, Arg.Any<CancellationToken>())
            .Returns(asset);

        SetupQuery(courseId, course);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Section");
    }

    [Fact]
    public async Task Handle_Should_AssignVideoAssetAndIncrementBitIndex_WhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var course = Course.Create(courseId, tenantId, "Math 101", "Desc", Guid.NewGuid()).Value;
        course.AddSection("Intro Section");
        var section = course.Sections.First();
        var sectionId = section.Id;

        var assetId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, assetId);
        var asset = VideoCourseAsset.Create(assetId, tenantId, arn, "Video Lecture", courseId).Value;
        asset.MarkAvailable();

        var command = new AssignAssetToCurriculumCommand(courseId, sectionId, assetId, "Lesson 1");
        _assetRepository.GetById(assetId, Arg.Any<CancellationToken>())
            .Returns(asset);

        SetupQuery(courseId, course);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        section.Items.Should().HaveCount(1);
        var item = section.Items.First();
        item.Id.Should().Be(result.Value);
        item.Title.Should().Be("Lesson 1");
        item.BitIndex.Should().Be(0);
        item.MainResourceType.Should().Be(ResourceType.Video);
        item.Resources.Should().HaveCount(1);
        item.Resources.First().CourseAssetId.Should().Be(assetId);

        course.NextAvailableBitIndex.Should().Be(1);
        _courseRepository.Received(1).Update(course);
    }
}
