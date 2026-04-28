using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.AddLesson;

public record AddLessonCommand(
    Guid CourseId, 
    Guid SectionId, 
    string Title, 
    Guid VideoId, 
    JsonElement? Metadata = null,
    Guid? LessonId = null) : ICommand<Success>;

public class AddLessonCommandValidator : AbstractValidator<AddLessonCommand>
{
    public AddLessonCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VideoId).NotEmpty();
    }
}

public sealed class AddLessonCommandHandler : IRequestHandler<AddLessonCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly IRequestClient<GetVideoMetadataRequest> _videoRequestClient;
    private readonly ILogger<AddLessonCommandHandler> _logger;

    public AddLessonCommandHandler(
        ICourseRepository courseRepository, 
        IRequestClient<GetVideoMetadataRequest> videoRequestClient,
        ILogger<AddLessonCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _videoRequestClient = videoRequestClient;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(AddLessonCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var metadata = request.Metadata;

        // If no metadata provided, try to fetch it from the Video module (Provider)
        if (metadata == null)
        {
            try
            {
                var response = await _videoRequestClient.GetResponse<VideoMetadataResponse, VideoMetaDataNotFoundResponse>(
                    new GetVideoMetadataRequest(request.VideoId), cancellationToken);
                
                if (response.Is<VideoMetadataResponse>(out var success))
                {
                    var msg = success.Message;
                    metadata = JsonSerializer.SerializeToElement(new
                    {
                        msg.Title,
                        msg.Status,
                        msg.Duration,
                        Url = msg.RelativeUrl
                    });
                    _logger.LogInformation("Fetched video metadata from Provider for Video {VideoId}.", request.VideoId);
                }
                else
                {
                    _logger.LogWarning("Video {VideoId} was not found by Provider. Initializing with empty metadata.", request.VideoId);
                    metadata = JsonDocument.Parse("{}").RootElement;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Fault during metadata handshake for Video {VideoId}. Error: {Error}", request.VideoId, ex.Message);
                metadata = JsonDocument.Parse("{}").RootElement;
            }
        }

        // If LessonId is provided, we are linking/updating an existing slot
        if (request.LessonId.HasValue)
        {
            var linkResult = course.LinkResourceToItem(request.LessonId.Value, request.VideoId);
            if (linkResult.IsError) return linkResult.Errors;

            course.UpdateResourceMetadata(request.VideoId, metadata.Value);
            _logger.LogInformation("Lesson {LessonId} updated with Video {VideoId} in Course {CourseId}.", request.LessonId, request.VideoId, request.CourseId);
        }
        else
        {
            // Otherwise, we are adding a brand new lesson item
            var result = course.AddLesson(request.SectionId, request.Title, request.VideoId, metadata.Value);
            if (result.IsError) return result.Errors;
            _logger.LogInformation("New Lesson '{Title}' added to Section {SectionId} in Course {CourseId}.", request.Title, request.SectionId, request.CourseId);
        }

        return Result.Success;
    }
}
