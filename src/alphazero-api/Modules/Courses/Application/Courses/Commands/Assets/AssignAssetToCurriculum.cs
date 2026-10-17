using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentValidation;
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

public class AssignAssetToCurriculumCommandValidator : AbstractValidator<AssignAssetToCurriculumCommand>
{
    public AssignAssetToCurriculumCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    }
}

public sealed class AssignAssetToCurriculumCommandHandler 
    : IRequestHandler<AssignAssetToCurriculumCommand, ErrorOr<Guid>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly IRepository<CourseAsset> _assetRepository;

    public AssignAssetToCurriculumCommandHandler(
        ICourseRepository courseRepository,
        IRepository<CourseAsset> assetRepository)
    {
        _courseRepository = courseRepository;
        _assetRepository = assetRepository;
    }

    public async Task<ErrorOr<Guid>> Handle(
        AssignAssetToCurriculumCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Course asset not found.");

        if (asset.CourseId != request.CourseId)
            return Error.Forbidden("CourseAsset.WrongCourse", "Asset does not belong to this course.");

        if (asset.State != CourseAssetState.Available)
            return Error.Conflict("CourseAsset.NotReady", $"Cannot assign asset in state '{asset.State}'. Must be Available.");

        var course = await _courseRepository.Query(request.CourseId)
            .WithSection(request.SectionId)
            .WithItems()
            .ExecuteAsync(ct);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var section = course.Sections.FirstOrDefault(s => s.Id == request.SectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        CurriculumResource resource = asset.CourseAssetType switch
        {
            CourseAssetType.Video => new VideoCurriculumResource(Guid.NewGuid(), 0, asset.Id),
            CourseAssetType.Document => new DocumentCurriculumResource(Guid.NewGuid(), 0, request.Title, asset.Id),
            CourseAssetType.Assessment => new AssessmentCurriculumResource(Guid.NewGuid(), 0, 70, asset.Id),
            _ => new ArticleCurriculumResource(Guid.NewGuid(), 0, request.Metadata ?? default, asset.Id)
        };

        ResourceType mainType = asset.CourseAssetType switch
        {
            CourseAssetType.Video => ResourceType.Video,
            CourseAssetType.Document => ResourceType.Document,
            CourseAssetType.Assessment => ResourceType.Assessment,
            _ => ResourceType.Article
        };

        var bitIndex = course.AllocateNextBitIndex();
        var itemResult = section.AddItem(
            Guid.NewGuid(),
            request.Title,
            mainType,
            bitIndex,
            new[] { resource });

        if (itemResult.IsError)
            return itemResult.Errors;

        _courseRepository.Update(course);
        return itemResult.Value.Id;
    }
}
