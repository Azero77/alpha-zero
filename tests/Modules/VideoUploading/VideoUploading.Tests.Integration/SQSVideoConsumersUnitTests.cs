using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class SQSVideoConsumersUnitTests
{
    private class FakeClock : IClock
    {
        public DateTime Now => new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
    }

    private Video CreateTestVideo(VideoStatus status = VideoStatus.Processing)
    {
        var clock = new FakeClock();
        var videoResult = Video.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Video",
            "Description",
            "raw/video.mp4",
            new VideoMetadata("test.mp4", "video/mp4", 1024, "FFmpeg", "None"),
            new ThumbnailInfo(null, null, false),
            clock);

        var video = videoResult.Value;
        if (status == VideoStatus.Published)
        {
            video.MarkAsLive("https://cdn.alphazero.cloud/streaming/tenant/video/master.m3u8", clock);
        }
        else if (status == VideoStatus.Failed)
        {
            video.MarkAsFailed();
        }

        return video;
    }

    [Fact]
    public async Task SQSVideoPublishedConsumer_Should_PublishVideo_WhenVideoIsProcessing()
    {
        // Arrange
        var video = CreateTestVideo(VideoStatus.Processing);
        var videoRepoMock = new Mock<IVideoRepository>();
        videoRepoMock.Setup(r => r.GetByIdAsync(video.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var moduleBusMock = new Mock<IModuleBus>();
        var clock = new FakeClock();
        var logger = NullLogger<SQSVideoPublishedConsumer>.Instance;

        var consumer = new SQSVideoPublishedConsumer(videoRepoMock.Object, unitOfWorkMock.Object, moduleBusMock.Object, clock, logger);

        var playbackUrl = "https://cdn.alphazero.cloud/streaming/tenant/video/master.m3u8";
        var targetResourceArn = "arn:alphazero:courses:tenant123:course/c1:lecture/l1";
        var queueMessage = new VideoPublishedQueueMessage(
            video.Id,
            video.TenantId,
            "SUCCESS",
            playbackUrl,
            "https://cdn.alphazero.cloud/streaming/tenant/video/thumbnails/poster.jpg",
            "00:10:30",
            1920,
            1080,
            "ffmpeg-fargate",
            targetResourceArn);

        var contextMock = new Mock<ConsumeContext<VideoPublishedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        video.Status.Should().Be(VideoStatus.Published);
        video.OutputFolder.Should().Be(playbackUrl);
        video.Specifications.Duration.Should().Be(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(30));
        video.Specifications.Resolution.width.Should().Be(1920);
        video.Specifications.Resolution.height.Should().Be(1080);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        moduleBusMock.Verify(x => x.Publish(
            It.Is<VideoPublishedEvent>(e =>
                e.VideoId == video.Id &&
                e.RelativeUrl == playbackUrl &&
                e.TargetResourceArn == targetResourceArn),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SQSVideoPublishedConsumer_Should_Skip_WhenVideoIsAlreadyPublished()
    {
        // Arrange
        var video = CreateTestVideo(VideoStatus.Published);
        var videoRepoMock = new Mock<IVideoRepository>();
        videoRepoMock.Setup(r => r.GetByIdAsync(video.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var moduleBusMock = new Mock<IModuleBus>();
        var clock = new FakeClock();
        var logger = NullLogger<SQSVideoPublishedConsumer>.Instance;

        var consumer = new SQSVideoPublishedConsumer(videoRepoMock.Object, unitOfWorkMock.Object, moduleBusMock.Object, clock, logger);

        var queueMessage = new VideoPublishedQueueMessage(
            video.Id,
            video.TenantId,
            "SUCCESS",
            "https://cdn.alphazero.cloud/new_url.m3u8",
            null,
            "00:05:00",
            1280,
            720,
            "ffmpeg-fargate",
            null);

        var contextMock = new Mock<ConsumeContext<VideoPublishedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert: Idempotent skip - no save changes and no duplicate event
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        moduleBusMock.Verify(x => x.Publish(It.IsAny<VideoPublishedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SQSVideoPublishedConsumer_Should_Skip_WhenVideoNotFound()
    {
        // Arrange
        var videoRepoMock = new Mock<IVideoRepository>();
        videoRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Video?)null);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var moduleBusMock = new Mock<IModuleBus>();
        var clock = new FakeClock();
        var logger = NullLogger<SQSVideoPublishedConsumer>.Instance;

        var consumer = new SQSVideoPublishedConsumer(videoRepoMock.Object, unitOfWorkMock.Object, moduleBusMock.Object, clock, logger);

        var queueMessage = new VideoPublishedQueueMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SUCCESS",
            "https://cdn.alphazero.cloud/master.m3u8",
            null,
            null,
            null,
            null,
            null,
            null);

        var contextMock = new Mock<ConsumeContext<VideoPublishedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        moduleBusMock.Verify(x => x.Publish(It.IsAny<VideoPublishedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SQSVideoProcessingFailedConsumer_Should_MarkAsFailed_AndPublishEvent()
    {
        // Arrange
        var video = CreateTestVideo(VideoStatus.Processing);
        var videoRepoMock = new Mock<IVideoRepository>();
        videoRepoMock.Setup(r => r.GetByIdAsync(video.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(video);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var moduleBusMock = new Mock<IModuleBus>();
        var logger = NullLogger<SQSVideoProcessingFailedConsumer>.Instance;

        var consumer = new SQSVideoProcessingFailedConsumer(videoRepoMock.Object, unitOfWorkMock.Object, moduleBusMock.Object, logger);

        var targetResourceArn = "arn:alphazero:courses:tenant123:course/c1";
        var queueMessage = new VideoProcessingFailedQueueMessage(
            video.Id,
            video.TenantId,
            "FAILED",
            new VideoProcessingErrorDetail("TranscodingException", "FFmpeg process exited with code 1"),
            targetResourceArn);

        var contextMock = new Mock<ConsumeContext<VideoProcessingFailedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        video.Status.Should().Be(VideoStatus.Failed);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        moduleBusMock.Verify(x => x.Publish(
            It.Is<VideoProcessingFailedEvent>(e =>
                e.VideoId == video.Id &&
                e.Reason == "FFmpeg process exited with code 1" &&
                e.TargetResourceArn == targetResourceArn),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SQSVideoProcessingFailedConsumer_Should_PublishEvent_EvenIfVideoNotFound()
    {
        // Arrange
        var videoId = Guid.NewGuid();
        var videoRepoMock = new Mock<IVideoRepository>();
        videoRepoMock.Setup(r => r.GetByIdAsync(videoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Video?)null);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var moduleBusMock = new Mock<IModuleBus>();
        var logger = NullLogger<SQSVideoProcessingFailedConsumer>.Instance;

        var consumer = new SQSVideoProcessingFailedConsumer(videoRepoMock.Object, unitOfWorkMock.Object, moduleBusMock.Object, logger);

        var queueMessage = new VideoProcessingFailedQueueMessage(
            videoId,
            Guid.NewGuid(),
            "FAILED",
            new VideoProcessingErrorDetail("States.TaskFailed", null),
            null);

        var contextMock = new Mock<ConsumeContext<VideoProcessingFailedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        moduleBusMock.Verify(x => x.Publish(
            It.Is<VideoProcessingFailedEvent>(e =>
                e.VideoId == videoId &&
                e.Reason == "States.TaskFailed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
