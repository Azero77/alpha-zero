using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.AddQuiz;

public record AddQuizCommand(Guid CourseId, Guid SectionId, string Title, Guid QuizId) : ICommand<Success>;

public class AddQuizCommandValidator : AbstractValidator<AddQuizCommand>
{
    public AddQuizCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.QuizId).NotEmpty();
    }
}

public sealed class AddQuizCommandHandler : IRequestHandler<AddQuizCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<AddQuizCommandHandler> _logger;

    public AddQuizCommandHandler(ICourseRepository courseRepository, ILogger<AddQuizCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(AddQuizCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.AddQuiz(request.SectionId, request.Title, request.QuizId);
        if (result.IsError) return result.Errors;

        _logger.LogInformation("Quiz '{Title}' added to Section {SectionId} in Course {CourseId}.", request.Title, request.SectionId, request.CourseId);

        return Result.Success;
    }
}
