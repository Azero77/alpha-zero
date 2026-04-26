using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
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
    private readonly ILogger<AddLessonCommandHandler> _logger;

    public AddLessonCommandHandler(ICourseRepository courseRepository, ILogger<AddLessonCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(AddLessonCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var metadata = request.Metadata ?? JsonDocument.Parse("{}").RootElement;

        // If LessonId is provided, we are linking/updating an existing slot
        if (request.LessonId.HasValue)
        {
            var linkResult = course.LinkResourceToItem(request.LessonId.Value, request.VideoId);
            if (linkResult.IsError) return linkResult.Errors;

            course.UpdateResourceMetadata(request.VideoId, metadata);
            _logger.LogInformation("Lesson {LessonId} updated with Video {VideoId} in Course {CourseId}.", request.LessonId, request.VideoId, request.CourseId);
        }
        else
        {
            // Otherwise, we are adding a brand new lesson item
            var result = course.AddLesson(request.SectionId, request.Title, request.VideoId, metadata);
            if (result.IsError) return result.Errors;
            _logger.LogInformation("New Lesson '{Title}' added to Section {SectionId} in Course {CourseId}.", request.Title, request.SectionId, request.CourseId);
        }

        return Result.Success;
    }
}
