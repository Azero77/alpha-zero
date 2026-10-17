using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;

// Commands
public record ReorderSectionsCommand(Guid CourseId, List<Guid> SectionIds) : ICommand<Success>;
public record ReorderItemsCommand(Guid CourseId, Guid SectionId, List<Guid> ItemIds) : ICommand<Success>;

// Validators
public class ReorderSectionsCommandValidator : AbstractValidator<ReorderSectionsCommand>
{
    public ReorderSectionsCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
                .WithMessage("SectionIds must not contain empty ids.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("SectionIds must not contain duplicate sections — every section must appear exactly once.");
    }
}

public class ReorderItemsCommandValidator : AbstractValidator<ReorderItemsCommand>
{
    public ReorderItemsCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.ItemIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
                .WithMessage("ItemIds must not contain empty ids.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("ItemIds must not contain duplicate items — every item must appear exactly once.");
    }
}

// Handlers
public sealed class ReorderCommandsHandlers : 
    IRequestHandler<ReorderSectionsCommand, ErrorOr<Success>>,
    IRequestHandler<ReorderItemsCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<ReorderCommandsHandlers> _logger;

    public ReorderCommandsHandlers(ICourseRepository courseRepository, ILogger<ReorderCommandsHandlers> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(ReorderSectionsCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSections()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.ReorderSections(request.SectionIds);
        if (!result.IsError) _logger.LogInformation("Sections reordered for Course {CourseId}.", request.CourseId);
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(ReorderItemsCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSection(request.SectionId)
            .WithItems()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.ReorderItems(request.SectionId, request.ItemIds);
        if (!result.IsError) _logger.LogInformation("Items reordered for Section {SectionId} in Course {CourseId}.", request.SectionId, request.CourseId);
        return result;
    }
}
