using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Delete;

public record DeleteCourseCommand(Guid CourseId) : ICommand<Success>;

public class DeleteCourseCommandValidator : AbstractValidator<DeleteCourseCommand>
{
    public DeleteCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class DeleteCourseCommandHandler(
    ICourseRepository courseRepository,
    ILogger<DeleteCourseCommandHandler> logger) : IRequestHandler<DeleteCourseCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await courseRepository.Query(request.CourseId).ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.Delete();
        if (result.IsError) return result.Errors;

        courseRepository.Update(course);
        logger.LogInformation("Course {CourseId} soft-deleted.", request.CourseId);

        return Result.Success;
    }
}
