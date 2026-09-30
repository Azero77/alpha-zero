using AlphaZero.Modules.VideoUploading.Application.Commands.FailVideoProcessing;
using AlphaZero.Modules.VideoUploading.Application.Commands.PublishVideo;
using AlphaZero.Modules.VideoUploading.Application.Commands.UpdateVideoProgress;
using AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.Application.Models;
using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class SQSVideoConsumersUnitTests
{
    [Fact]
    public async Task SQSVideoPublishedConsumer_Should_Dispatch_CompleteVideoPublishingCommand()
    {
        var moduleMock = new Mock<AlphaZero.Modules.VideoUploading.Application.IVideoUploadingModule>();
        var logger = NullLogger<SQSVideoPublishedConsumer>.Instance;
        var notifierMock = new Mock<AlphaZero.Modules.VideoUploading.Application.Services.IVideoProgressNotifier>();
        var consumer = new SQSVideoPublishedConsumer(moduleMock.Object, logger, notifierMock.Object);

        var queueMessage = new VideoPublishedQueueMessage(
            Guid.NewGuid(), Guid.NewGuid(), "SUCCESS", "https://cdn/playback.m3u8", null, "00:10:30", 1920, 1080, "ffmpeg", "arn");

        var contextMock = new Mock<ConsumeContext<VideoPublishedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);

        await consumer.Consume(contextMock.Object);

        moduleMock.Verify(m => m.Send(
            It.Is<CompleteVideoPublishingCommand>(c => c.VideoId == queueMessage.VideoId && c.PlaybackUrl == queueMessage.PlaybackUrl),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SQSVideoProcessingFailedConsumer_Should_Dispatch_FailVideoProcessingCommand()
    {
        var moduleMock = new Mock<AlphaZero.Modules.VideoUploading.Application.IVideoUploadingModule>();
        var logger = NullLogger<SQSVideoProcessingFailedConsumer>.Instance;
        var notifierMock = new Mock<AlphaZero.Modules.VideoUploading.Application.Services.IVideoProgressNotifier>();
        var consumer = new SQSVideoProcessingFailedConsumer(moduleMock.Object, logger, notifierMock.Object);

        var queueMessage = new VideoProcessingFailedQueueMessage(
            Guid.NewGuid(), Guid.NewGuid(), "FAILED", new VideoProcessingErrorDetail("Error", "Cause"), "arn");

        var contextMock = new Mock<ConsumeContext<VideoProcessingFailedQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);

        await consumer.Consume(contextMock.Object);

        moduleMock.Verify(m => m.Send(
            It.Is<FailVideoProcessingCommand>(c => c.VideoId == queueMessage.VideoId && c.Reason == "Cause"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SQSVideoProgressConsumer_Should_Dispatch_UpdateVideoProgressCommand()
    {
        var moduleMock = new Mock<AlphaZero.Modules.VideoUploading.Application.IVideoUploadingModule>();
        var logger = NullLogger<SQSVideoProgressConsumer>.Instance;
        var consumer = new SQSVideoProgressConsumer(moduleMock.Object, logger);

        var queueMessage = new VideoProgressQueueMessage(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Analyzing", "IN_PROGRESS", "Details");

        var contextMock = new Mock<ConsumeContext<VideoProgressQueueMessage>>();
        contextMock.Setup(x => x.Message).Returns(queueMessage);

        await consumer.Consume(contextMock.Object);

        moduleMock.Verify(m => m.Send(
            It.Is<UpdateVideoProgressCommand>(c => c.VideoId.ToString() == queueMessage.VideoId && c.Stage == PipelineStage.Analyzing),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
