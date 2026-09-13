using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class SyncResourceMetadataTests
{
    private readonly IRepository<CourseAsset> _assetRepository = Substitute.For<IRepository<CourseAsset>>();
    private readonly ILogger<SyncResourceMetadataCommandHandler> _handlerLogger = Substitute.For<ILogger<SyncResourceMetadataCommandHandler>>();
    private readonly ILogger<VideoCourseMetadataSyncCommandHandler> _videoLogger = Substitute.For<ILogger<VideoCourseMetadataSyncCommandHandler>>();
    private readonly ILogger<DocumentCourseMetadataSyncCommandHandler> _docLogger = Substitute.For<ILogger<DocumentCourseMetadataSyncCommandHandler>>();
    private readonly ILogger<AssessmentCourseMetadataSyncCommandHandler> _assessmentLogger = Substitute.For<ILogger<AssessmentCourseMetadataSyncCommandHandler>>();

    private readonly VideoCourseMetadataSyncCommandHandler _videoStrategy;
    private readonly DocumentCourseMetadataSyncCommandHandler _docStrategy;
    private readonly AssessmentCourseMetadataSyncCommandHandler _assessmentStrategy;
    private readonly SyncResourceMetadataCommandHandler _handler;

    public SyncResourceMetadataTests()
    {
        _videoStrategy = new VideoCourseMetadataSyncCommandHandler(_videoLogger);
        _docStrategy = new DocumentCourseMetadataSyncCommandHandler(_docLogger);
        _assessmentStrategy = new AssessmentCourseMetadataSyncCommandHandler(_assessmentLogger);

        _handler = new SyncResourceMetadataCommandHandler(
            _assetRepository,
            new ICourseMetadataSyncCommandHandler[] { _videoStrategy, _docStrategy, _assessmentStrategy },
            _handlerLogger);
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenAssetDoesNotExist()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        _assetRepository.GetById(resourceId, Arg.Any<CancellationToken>()).Returns((CourseAsset?)null);
        var command = new SyncResourceMetadataCommand(resourceId, JsonDocument.Parse("{}").RootElement);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        _assetRepository.DidNotReceive().Update(Arg.Any<CourseAsset>());
    }

    [Fact]
    public async Task Handle_Should_UpdateTitle_WhenTitleIsProvidedInMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, arn, "Original Title", courseId).Value;

        _assetRepository.GetById(videoId, Arg.Any<CancellationToken>()).Returns(asset);

        var payload = JsonDocument.Parse("""{"Title": "Updated Title", "duration": "00:15:00"}""").RootElement;
        var command = new SyncResourceMetadataCommand(videoId, payload);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        asset.Title.Should().Be("Updated Title");
        _assetRepository.Received(1).Update(asset);
    }

    [Fact]
    public async Task Handle_Should_SupportCamelCaseTitle_WhenProvidedInMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, arn, "Old Title", courseId).Value;

        _assetRepository.GetById(videoId, Arg.Any<CancellationToken>()).Returns(asset);

        var payload = JsonDocument.Parse("""{"title": "Camel Case Title"}""").RootElement;
        var command = new SyncResourceMetadataCommand(videoId, payload);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        asset.Title.Should().Be("Camel Case Title");
        _assetRepository.Received(1).Update(asset);
    }

    [Fact]
    public async Task VideoStrategy_Should_UpdateStreamingInfo_FromPascalAndCamelCase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, arn, "Video", courseId).Value;

        var payload = JsonDocument.Parse("""
        {
            "ThumbnailUrl": "https://cdn.example.com/thumb.jpg",
            "RelativeStreamingUrl": "streams/master.m3u8",
            "Duration": "00:22:30"
        }
        """).RootElement;

        // Act
        await _videoStrategy.Sync(asset, arn, payload);

        // Assert
        asset.ThumbnailUrl.Should().Be("https://cdn.example.com/thumb.jpg");
        asset.RelativeStreamingUrl.Should().Be("streams/master.m3u8");
        asset.Duration.Should().Be(TimeSpan.FromMinutes(22).Add(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task VideoStrategy_Should_FallBackToRawProperties_WhenAlternativeKeysProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, arn, "Video", courseId).Value;

        var payload = JsonDocument.Parse("""
        {
            "thumbnailUrl": "https://cdn.example.com/thumb2.jpg",
            "relativeUrl": "streams/alt.m3u8",
            "duration": "01:05:00"
        }
        """).RootElement;

        // Act
        await _videoStrategy.Sync(asset, arn, payload);

        // Assert
        asset.ThumbnailUrl.Should().Be("https://cdn.example.com/thumb2.jpg");
        asset.RelativeStreamingUrl.Should().Be("streams/alt.m3u8");
        asset.Duration.Should().Be(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task VideoStrategy_Should_NotThrow_WhenAssetIsNotVideo()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForDocument(tenantId, docId);
        var asset = DocumentCourseAsset.Create(docId, tenantId, arn, "Doc", courseId, "file.pdf", 100, "application/pdf").Value;

        var payload = JsonDocument.Parse("{}").RootElement;

        // Act
        var act = async () => await _videoStrategy.Sync(asset, arn, payload);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DocumentStrategy_Should_UpdateFileInfo_FromPayload()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForDocument(tenantId, docId);
        var asset = DocumentCourseAsset.Create(docId, tenantId, arn, "Doc", courseId, "old.pdf", 100, "application/pdf").Value;

        var payload = JsonDocument.Parse("""
        {
            "FileName": "syllabus_v2.pdf",
            "FileSize": 2097152,
            "ContentType": "application/pdf"
        }
        """).RootElement;

        // Act
        await _docStrategy.Sync(asset, arn, payload);

        // Assert
        asset.FileName.Should().Be("syllabus_v2.pdf");
        asset.Size.Should().Be(2097152);
        asset.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task DocumentStrategy_Should_FallBackToSizeAndCamelCaseProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForDocument(tenantId, docId);
        var asset = DocumentCourseAsset.Create(docId, tenantId, arn, "Doc", courseId, "old.pdf", 100, "application/pdf").Value;

        var payload = JsonDocument.Parse("""
        {
            "fileName": "lecture_slides.pptx",
            "size": 5242880,
            "contentType": "application/vnd.ms-powerpoint"
        }
        """).RootElement;

        // Act
        await _docStrategy.Sync(asset, arn, payload);

        // Assert
        asset.FileName.Should().Be("lecture_slides.pptx");
        asset.Size.Should().Be(5242880);
        asset.ContentType.Should().Be("application/vnd.ms-powerpoint");
    }

    [Fact]
    public async Task DocumentStrategy_Should_NotThrow_WhenAssetIsNotDocument()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, arn, "Video", courseId).Value;

        var payload = JsonDocument.Parse("{}").RootElement;

        // Act
        var act = async () => await _docStrategy.Sync(asset, arn, payload);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AssessmentStrategy_Should_UpdateAssessmentInfo_FromPayload()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForAssessment(tenantId, assessmentId);
        var asset = AssessmentCourseAsset.Create(assessmentId, tenantId, arn, "Quiz", courseId, 5, AssessmentType.Practice).Value;

        var payload = JsonDocument.Parse("""
        {
            "questionsNumber": 20,
            "type": "Midterm"
        }
        """).RootElement;

        // Act
        await _assessmentStrategy.Sync(asset, arn, payload);

        // Assert
        asset.QuestionsNumber.Should().Be(20);
        asset.Type.Should().Be(AssessmentType.Midterm);
    }

    [Fact]
    public async Task AssessmentStrategy_Should_ParseCaseInsensitiveEnumAndQuestionNumberFallback()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForAssessment(tenantId, assessmentId);
        var asset = AssessmentCourseAsset.Create(assessmentId, tenantId, arn, "Quiz", courseId, 5, AssessmentType.Practice).Value;

        var payload = JsonDocument.Parse("""
        {
            "questionNumber": 35,
            "type": "final"
        }
        """).RootElement;

        // Act
        await _assessmentStrategy.Sync(asset, arn, payload);

        // Assert
        asset.QuestionsNumber.Should().Be(35);
        asset.Type.Should().Be(AssessmentType.Final);
    }

    [Fact]
    public async Task AssessmentStrategy_Should_NotThrow_WhenAssetIsNotAssessment()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(tenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, tenantId, arn, "Video", courseId).Value;

        var payload = JsonDocument.Parse("{}").RootElement;

        // Act
        var act = async () => await _assessmentStrategy.Sync(asset, arn, payload);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_Should_CoordinateEntireSyncPipeline_ForAssessment()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var arn = ResourceArn.ForAssessment(tenantId, assessmentId);
        var asset = AssessmentCourseAsset.Create(assessmentId, tenantId, arn, "Old Assessment Title", courseId, 10, AssessmentType.Practice).Value;

        _assetRepository.GetById(assessmentId, Arg.Any<CancellationToken>()).Returns(asset);

        var payload = JsonDocument.Parse("""
        {
            "title": "Comprehensive Final Exam",
            "questionsNumber": 50,
            "type": "Final"
        }
        """).RootElement;

        var command = new SyncResourceMetadataCommand(assessmentId, payload);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        asset.Title.Should().Be("Comprehensive Final Exam");
        asset.QuestionsNumber.Should().Be(50);
        asset.Type.Should().Be(AssessmentType.Final);
        _assetRepository.Received(1).Update(asset);
    }
}
