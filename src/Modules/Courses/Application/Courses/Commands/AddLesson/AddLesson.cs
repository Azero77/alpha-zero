using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.AddLesson;

public record AddLessonCommand(Guid CourseId, Guid SectionId, string Title, Guid VideoId) : ICommand<Success>;

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

        var result = course.AddLesson(request.SectionId, request.Title, request.VideoId);
        if (result.IsError) return result.Errors;

        _logger.LogInformation("Lesson '{Title}' added to Section {SectionId} in Course {CourseId}.", request.Title, request.SectionId, request.CourseId);

        return Result.Success;
    }
}
