using AlphaZero.Modules.Courses.Application;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Videos;

/// <summary>
/// Video upload started → Create a Pending VideoCourseAsset in the course pool.
/// Only fires if TargetResourceArn contains a courseId (Flow 1: course-level upload).
/// </summary>
public class VideoUploadingStartedEventHandler : IConsumer<UploadVideoRequestedEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<VideoUploadingStartedEventHandler> _logger;

    public VideoUploadingStartedEventHandler(
        ICoursesModule coursesModule, 
        ILogger<VideoUploadingStartedEventHandler> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UploadVideoRequestedEvent> context)
    {
        var msg = context.Message;

        if (string.IsNullOrEmpty(msg.TargetResourceArn))
            return;

        var arnResult = ResourceArn.Create(msg.TargetResourceArn);
        if (arnResult.IsError) return;

        var courseId = arnResult.Value.ExtractCourseId();
        if (courseId is null) return;

        var command = new CreateVideoCourseAssetCommand(
            msg.VideoId, msg.TenantId, courseId.Value, 
            $"Video {msg.VideoId:N}");

        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
            _logger.LogWarning("Failed to create VideoCourseAsset for Video {VideoId}: {Errors}", 
                msg.VideoId, string.Join(", ", result.Errors.Select(e => e.Description)));
        else
            _logger.LogInformation("Created Pending VideoCourseAsset for Video {VideoId} in Course {CourseId}.", 
                msg.VideoId, courseId.Value);
    }
}

/// <summary>
/// Video processing failed → Mark the CourseAsset as Failed.
/// </summary>
public class VideoUploadingFailedEventHandler : IConsumer<VideoProcessingFailedEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<VideoUploadingFailedEventHandler> _logger;

    public VideoUploadingFailedEventHandler(
        ICoursesModule coursesModule,
        ILogger<VideoUploadingFailedEventHandler> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProcessingFailedEvent> context)
    {
        var msg = context.Message;

        var command = new MarkCourseAssetFailedCommand(msg.VideoId, msg.Reason);
        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
            _logger.LogWarning("Failed to mark CourseAsset {VideoId} as failed: {Errors}", 
                msg.VideoId, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}

/// <summary>
/// Video published (transcoding done) → Mark the CourseAsset as Available via strategy payload.
/// TODO (Broker Selection): When a production message broker (e.g. SQS/SNS, RabbitMQ) is configured,
/// route these lifecycle events using topics (e.g. "courses.video") so only the courses queue consumes them.
/// </summary>
public class VideoUploadedEventHandler : IConsumer<VideoPublishedEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ILogger<VideoUploadedEventHandler> _logger;

    public VideoUploadedEventHandler(
        ICoursesModule coursesModule,
        ILogger<VideoUploadedEventHandler> logger)
    {
        _coursesModule = coursesModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedEvent> context)
    {
        var msg = context.Message;

        if (string.IsNullOrEmpty(msg.TargetResourceArn))
            return;

        var arnResult = ResourceArn.Create(msg.TargetResourceArn);
        if (arnResult.IsError) return;

        var courseId = arnResult.Value.ExtractCourseId();
        if (courseId is null) return;

        var payload = JsonSerializer.SerializeToElement(new
        {
            relativeUrl = msg.RelativeUrl
        });

        var command = new MarkCourseAssetAvailableCommand(msg.VideoId, payload);
        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
            _logger.LogWarning("Failed to mark CourseAsset {VideoId} as available: {Errors}", 
                msg.VideoId, string.Join(", ", result.Errors.Select(e => e.Description)));
        else
            _logger.LogInformation("CourseAsset {VideoId} is now Available in pool.", msg.VideoId);
    }
}
