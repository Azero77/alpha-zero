using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;

// Commands
public record ReorderSectionsCommand(Guid CourseId, List<Guid> SectionIds) : ICommand<Success>;
public record ReorderItemsCommand(Guid CourseId, Guid SectionId, List<Guid> ItemIds) : ICommand<Success>;

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
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.ReorderSections(request.SectionIds);
        if (!result.IsError) _logger.LogInformation("Sections reordered for Course {CourseId}.", request.CourseId);
        return result;
    }

    public async Task<ErrorOr<Success>> Handle(ReorderItemsCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.ReorderItems(request.SectionId, request.ItemIds);
        if (!result.IsError) _logger.LogInformation("Items reordered for Section {SectionId} in Course {CourseId}.", request.SectionId, request.CourseId);
        return result;
    }
}
