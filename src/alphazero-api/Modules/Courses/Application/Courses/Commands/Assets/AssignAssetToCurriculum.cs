using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record AssignAssetToCurriculumCommand(
    Guid CourseId,
    Guid SectionId,
    Guid AssetId,
    string Title,
    JsonElement? Metadata = null
) : ICommand<Guid>;

public sealed class AssignAssetToCurriculumCommandHandler 
    : IRequestHandler<AssignAssetToCurriculumCommand, ErrorOr<Guid>>
{
    private readonly ICourseRepository _courseRepository;

    public AssignAssetToCurriculumCommandHandler(ICourseRepository courseRepository)
        => _courseRepository = courseRepository;

    public async Task<ErrorOr<Guid>> Handle(
        AssignAssetToCurriculumCommand request, CancellationToken ct)
    {
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, ct);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var metadata = request.Metadata ?? JsonDocument.Parse("{}").RootElement;
        var itemResult = course.AssignAssetToCurriculum(request.AssetId, request.SectionId, request.Title, metadata);
        if (itemResult.IsError) return itemResult.Errors;

        _courseRepository.Update(course);
        return itemResult.Value.Id;
    }
}
