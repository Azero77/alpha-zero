using AlphaZero.Modules.Courses.Application.Courses.Commands.AddLesson;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Videos;

public class AutoLinkVideoToCourseConsumer : IConsumer<VideoPublishedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<AutoLinkVideoToCourseConsumer> _logger;

    public AutoLinkVideoToCourseConsumer(IMediator mediator, ILogger<AutoLinkVideoToCourseConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedEvent> context)
    {
        var msg = context.Message;
        if (string.IsNullOrEmpty(msg.TargetResourceArn)) return;

        var arnResult = ResourceArn.Create(msg.TargetResourceArn);
        if (arnResult.IsError || arnResult.Value.Service != "courses") return;

        var pathParts = arnResult.Value.ResourcePath.Split('/');
        
        // Expected Course Format: az:courses:{tenant}:course/{courseId}/section/{sectionId}
        // Expected Lesson Format: az:courses:{tenant}:course/{courseId}/section/{sectionId}/lesson/{lessonId}
        
        if (pathParts.Length < 4 || pathParts[0] != "course" || pathParts[2] != "section") return;

        Guid courseId = Guid.Parse(pathParts[1]);
        Guid sectionId = Guid.Parse(pathParts[3]);
        Guid? lessonId = null;

        if (pathParts.Length >= 6 && pathParts[4] == "lesson")
        {
            lessonId = Guid.Parse(pathParts[5]);
        }

        var metadata = JsonSerializer.SerializeToElement(new {
            Url = msg.RelativeUrl,
            Status = "Ready",
            VideoId = msg.VideoId
        });

        var command = new AddLessonCommand(
            courseId, 
            sectionId, 
            "Auto-Linked Video", // Fallback title
            msg.VideoId, 
            metadata, 
            lessonId);

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Auto-link command failed for Video {VideoId}: {Error}", 
                msg.VideoId, result.FirstError.Description);
        }
        else
        {
            _logger.LogInformation("Successfully processed auto-link for Video {VideoId} to {Arn}", 
                msg.VideoId, msg.TargetResourceArn);
        }
    }
}
