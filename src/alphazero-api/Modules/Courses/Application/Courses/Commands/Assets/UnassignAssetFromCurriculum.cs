using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record UnassignAssetFromCurriculumCommand(
    Guid CourseId,
    Guid ItemId
) : ICommand<Success>;

public sealed class UnassignAssetFromCurriculumCommandHandler 
    : IRequestHandler<UnassignAssetFromCurriculumCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;

    public UnassignAssetFromCurriculumCommandHandler(ICourseRepository courseRepository)
        => _courseRepository = courseRepository;

    public async Task<ErrorOr<Success>> Handle(
        UnassignAssetFromCurriculumCommand request, CancellationToken ct)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSections()
            .WithItems()
            .ExecuteAsync(ct);
        if (course is null) return Error.NotFound("Course.NotFound");

        var section = course.Sections.FirstOrDefault(s => s.Items.Any(i => i.Id == request.ItemId && !i.IsDeleted) && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Section.NotFound", "No section contains this item.");

        // Single domain operation: captures the BitIndex, soft-deletes the item and raises
        // CurriculumItemDeletedDomainEvent so the mask bit is deactivated in the same transaction.
        var removeResult = course.RemoveItem(section.Id, request.ItemId);
        if (removeResult.IsError)
            return removeResult.Errors;

        _courseRepository.Update(course);
        return Result.Success;
    }
}
