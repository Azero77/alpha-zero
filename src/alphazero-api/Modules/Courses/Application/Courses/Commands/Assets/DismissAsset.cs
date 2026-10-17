using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record DismissAssetCommand(Guid CourseId, Guid AssetId) : ICommand<Success>;

public class DismissAssetCommandValidator : AbstractValidator<DismissAssetCommand>
{
    public DismissAssetCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
    }
}

public sealed class DismissAssetCommandHandler 
    : IRequestHandler<DismissAssetCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly ICourseRepository _courseRepository;

    public DismissAssetCommandHandler(
        IRepository<CourseAsset> assetRepository,
        ICourseRepository courseRepository)
    {
        _assetRepository = assetRepository;
        _courseRepository = courseRepository;
    }

    public async Task<ErrorOr<Success>> Handle(
        DismissAssetCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null) return Error.NotFound("CourseAsset.NotFound");
        if (asset.CourseId != request.CourseId)
            return Error.Forbidden("CourseAsset.WrongCourse", "Asset does not belong to this course.");

        if (await _courseRepository.IsAssetInUseAsync(request.AssetId, ct))
            return Error.Conflict("CourseAsset.InUse", "Cannot dismiss an asset currently in use by curriculum items.");

        var result = asset.Archive();
        if (result.IsError) return result.Errors;

        _assetRepository.Update(asset);
        return Result.Success;
    }
}
