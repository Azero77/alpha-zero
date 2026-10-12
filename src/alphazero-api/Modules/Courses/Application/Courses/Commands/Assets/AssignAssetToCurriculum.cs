using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
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

        var section = course.Sections.FirstOrDefault(s => s.Id == request.SectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var assignResult = course.AssignAsset(request.AssetId);
        if (assignResult.IsError)
            return assignResult.Errors;

        var asset = assignResult.Value;
        CurriculumResource resource = asset.CourseAssetType switch
        {
            CourseAssetType.Video => new VideoCurriculumResource(Guid.NewGuid(), 0, asset.Id, asset.Id),
            CourseAssetType.Document => new DocumentCurriculumResource(Guid.NewGuid(), 0, asset.Id, request.Title, courseAssetId: asset.Id),
            CourseAssetType.Assessment => new AssessmentCurriculumResource(Guid.NewGuid(), 0, courseAssetId: asset.Id),
            _ => new ArticleCurriculumResource(Guid.NewGuid(), 0, request.Title, asset.Id)
        };

        var bitIndex = course.AllocateNextBitIndex();
        var itemResult = section.AddItem(
            Guid.NewGuid(),
            course.TenantId,
            request.Title,
            asset.CourseAssetType.ToString(),
            bitIndex,
            new[] { resource });

        if (itemResult.IsError)
        {
            course.ReleaseAsset(request.AssetId);
            return itemResult.Errors;
        }

        _courseRepository.Update(course);
        return itemResult.Value.Id;
    }
}
