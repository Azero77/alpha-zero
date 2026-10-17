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
    IEnrollmentRepository enrollmentRepository,
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
        // The full section graph (not just the target section) must be loaded: the
        // post-delete ActiveTrackedItems recount happens over the aggregate's loaded sections.
        var course = await courseRepository.Query(request.CourseId)
            .WithSections()
            .WithItems()
            .ExecuteAsync(cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.DeleteSection(request.SectionId);
        if (result.IsError)
            return result.Errors;

        // Progress is a bitmask over item indexes — the earned bits are stable across
        // section deletion (BitmaskStabilityTests guarantee this); only the active
        // denominator changes, so every enrollment's percentage recalculates against
        // the remaining active items instead of staying locked below 100%.
        var enrollments = await enrollmentRepository.Get(e => e.CourseId == request.CourseId, cancellationToken);
        foreach (var enrollment in enrollments)
        {
            enrollment.UpdateActiveItems(course.ActiveTrackedItems);
            enrollmentRepository.Update(enrollment);
        }

        courseRepository.Update(course);
        logger.LogInformation("Soft-deleted section {SectionId} in course {CourseId} (recalculated progress for {EnrollmentCount} enrollments).",
            request.SectionId, request.CourseId, enrollments.Count);

        return Result.Success;
    }
}
