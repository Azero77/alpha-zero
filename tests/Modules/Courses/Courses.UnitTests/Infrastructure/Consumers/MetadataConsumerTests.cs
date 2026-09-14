using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using AlphaZero.Modules.Courses.Infrastructure.Consumers.Videos;
using AlphaZero.Modules.Documents.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers;

public class MetadataConsumerTests
{
    private readonly Mock<ICoursesModule> _coursesModuleMock;
    private readonly Mock<ILogger<VideoUploadedEventHandler>> _videoUploadedLoggerMock;
    private readonly Mock<ILogger<MarkCourseAssetFailedCommandHandler>> _failedHandlerLoggerMock;

    public MetadataConsumerTests()
    {
        _coursesModuleMock = new Mock<ICoursesModule>();
        _videoUploadedLoggerMock = new Mock<ILogger<VideoUploadedEventHandler>>();
        _failedHandlerLoggerMock = new Mock<ILogger<MarkCourseAssetFailedCommandHandler>>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task VideoUploadedEventHandler_Should_ReturnEarly_WhenTargetResourceArnIsNullOrEmpty(string? arn)
    {
        // Arrange
        var consumer = new VideoUploadedEventHandler(_coursesModuleMock.Object, _videoUploadedLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<VideoPublishedEvent>>();
        var msg = new VideoPublishedEvent(Guid.NewGuid(), "master.m3u8", arn);
        contextMock.Setup(x => x.Message).Returns(msg);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(It.IsAny<MarkCourseAssetAvailableCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VideoUploadedEventHandler_Should_ReturnEarly_WhenTargetResourceArnIsNotCourse()
    {
        // Arrange
        var consumer = new VideoUploadedEventHandler(_coursesModuleMock.Object, _videoUploadedLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<VideoPublishedEvent>>();
        // ResourceArn for video (not course)
        var nonCourseArn = ResourceArn.ForVideo(Guid.NewGuid(), Guid.NewGuid()).Value;
        var msg = new VideoPublishedEvent(Guid.NewGuid(), "master.m3u8", nonCourseArn);
        contextMock.Setup(x => x.Message).Returns(msg);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(It.IsAny<MarkCourseAssetAvailableCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VideoUploadedEventHandler_Should_SendCommand_WhenTargetResourceArnIsCourse()
    {
        // Arrange
        var consumer = new VideoUploadedEventHandler(_coursesModuleMock.Object, _videoUploadedLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<VideoPublishedEvent>>();
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var courseArn = ResourceArn.ForCourse(tenantId, courseId).Value;
        var msg = new VideoPublishedEvent(videoId, "master.m3u8", courseArn);
        contextMock.Setup(x => x.Message).Returns(msg);

        _coursesModuleMock.Setup(x => x.Send(It.IsAny<MarkCourseAssetAvailableCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(
            It.Is<MarkCourseAssetAvailableCommand>(cmd => cmd.AssetId == videoId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkCourseAssetFailedCommandHandler_Should_ReturnSuccess_WhenAssetNotFound()
    {
        // Arrange (D6 verification)
        var repoMock = new Mock<IRepository<CourseAsset>>();
        repoMock.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseAsset?)null);

        var handler = new MarkCourseAssetFailedCommandHandler(repoMock.Object, _failedHandlerLoggerMock.Object);
        var command = new MarkCourseAssetFailedCommand(Guid.NewGuid(), "Transcoding failed");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        repoMock.Verify(r => r.Update(It.IsAny<CourseAsset>()), Times.Never);
    }

    [Fact]
    public async Task VideoMetadataChangedConsumer_Should_DispatchSyncResourceMetadataCommand()
    {
        // Arrange
        var consumer = new VideoMetadataChangedConsumer(_coursesModuleMock.Object);
        var contextMock = new Mock<ConsumeContext<VideoMetadataChangedIntegrationEvent>>();
        var videoId = Guid.NewGuid();
        var msg = new VideoMetadataChangedIntegrationEvent(videoId, "Updated Video", "New Description");
        contextMock.Setup(x => x.Message).Returns(msg);

        _coursesModuleMock.Setup(x => x.Send(It.IsAny<SyncResourceMetadataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(
            It.Is<SyncResourceMetadataCommand>(cmd =>
                cmd.ResourceId == videoId &&
                cmd.Metadata.GetProperty("Title").GetString() == "Updated Video" &&
                cmd.Metadata.GetProperty("Description").GetString() == "New Description"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DocumentMetadataChangedConsumer_Should_DispatchSyncResourceMetadataCommand()
    {
        // Arrange
        var consumer = new DocumentMetadataChangedConsumer(_coursesModuleMock.Object);
        var contextMock = new Mock<ConsumeContext<DocumentMetadataChangedIntegrationEvent>>();
        var documentId = Guid.NewGuid();
        var msg = new DocumentMetadataChangedIntegrationEvent(documentId, "Updated Document", "Doc Description", "pdf", 2048);
        contextMock.Setup(x => x.Message).Returns(msg);

        _coursesModuleMock.Setup(x => x.Send(It.IsAny<SyncResourceMetadataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(
            It.Is<SyncResourceMetadataCommand>(cmd =>
                cmd.ResourceId == documentId &&
                cmd.Metadata.GetProperty("Title").GetString() == "Updated Document" &&
                cmd.Metadata.GetProperty("Description").GetString() == "Doc Description" &&
                cmd.Metadata.GetProperty("FileSize").GetInt64() == 2048 &&
                cmd.Metadata.GetProperty("ContentType").GetString() == "pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssessmentMetadataChangedConsumer_Should_DispatchSyncResourceMetadataCommand()
    {
        // Arrange
        var consumer = new AssessmentMetadataChangedConsumer(_coursesModuleMock.Object);
        var contextMock = new Mock<ConsumeContext<AssessmentMetadataChangedIntegrationEvent>>();
        var assessmentId = Guid.NewGuid();
        var msg = new AssessmentMetadataChangedIntegrationEvent(assessmentId, "Updated Assessment", "Practice", 80, "Published");
        contextMock.Setup(x => x.Message).Returns(msg);

        _coursesModuleMock.Setup(x => x.Send(It.IsAny<SyncResourceMetadataCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _coursesModuleMock.Verify(x => x.Send(
            It.Is<SyncResourceMetadataCommand>(cmd =>
                cmd.ResourceId == assessmentId &&
                cmd.Metadata.GetProperty("Title").GetString() == "Updated Assessment" &&
                cmd.Metadata.GetProperty("Type").GetString() == "Practice" &&
                cmd.Metadata.GetProperty("PassingScore").GetDecimal() == 80 &&
                cmd.Metadata.GetProperty("Status").GetString() == "Published"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
