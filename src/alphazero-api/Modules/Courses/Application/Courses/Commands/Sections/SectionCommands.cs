using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Sections;

public record RenameCourseSectionCommand(Guid CourseId, Guid SectionId, string Title) : ICommand<Success>;

public record DeleteCourseSectionCommand(Guid CourseId, Guid SectionId) : ICommand<Success>;

public class RenameCourseSectionCommandValidator : AbstractValidator<RenameCourseSectionCommand>
{
    public RenameCourseSectionCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    }
}

public class DeleteCourseSectionCommandValidator : AbstractValidator<DeleteCourseSectionCommand>
{
    public DeleteCourseSectionCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
    }
}

public sealed class CourseSectionHandlers(
    ICourseRepository courseRepository,
    ILogger<CourseSectionHandlers> logger) :
    IRequestHandler<RenameCourseSectionCommand, ErrorOr<Success>>,
    IRequestHandler<DeleteCourseSectionCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        RenameCourseSectionCommand request, CancellationToken cancellationToken)
    {
        var course = await courseRepository.Query(request.CourseId)
            .WithSection(request.SectionId)
            .ExecuteAsync(cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.UpdateSectionTitle(request.SectionId, request.Title);
        if (result.IsError)
            return result.Errors;

        courseRepository.Update(course);
        logger.LogInformation("Renamed section {SectionId} in course {CourseId}.", request.SectionId, request.CourseId);

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> Handle(
        DeleteCourseSectionCommand request, CancellationToken cancellationToken)
    {
        // The full section graph must be loaded: DeleteSection captures the BitIndex of every
        // contained curriculum item before the section (and its items) are soft-deleted.
        var course = await courseRepository.Query(request.CourseId)
            .WithSections()
            .WithItems()
            .ExecuteAsync(cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.DeleteSection(request.SectionId);
        if (result.IsError)
            return result.Errors;

        // CourseSectionDeletedDomainEvent carries the captured BitIndexes; the
        // CourseActiveItemsDomainEventHandler deactivates exactly those positions on the course
        // mask in the same SaveChangesAsync transaction. Enrollment masks are NEVER replicated
        // here: completion percentages derive from the course mask at calculation time, so no
        // enrollment needs to be touched (no per-enrollment UPDATE storm, see ProgressCalculation).
        courseRepository.Update(course);
        logger.LogInformation("Soft-deleted section {SectionId} in course {CourseId}.",
            request.SectionId, request.CourseId);

        return Result.Success;
    }
}
