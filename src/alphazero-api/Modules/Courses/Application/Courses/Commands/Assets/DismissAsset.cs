using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record DismissAssetCommand(Guid CourseId, Guid AssetId) : ICommand<Success>;

public sealed class DismissAssetCommandHandler 
    : IRequestHandler<DismissAssetCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;

    public DismissAssetCommandHandler(IRepository<CourseAsset> assetRepository)
        => _assetRepository = assetRepository;

    public async Task<ErrorOr<Success>> Handle(
        DismissAssetCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null) return Error.NotFound("CourseAsset.NotFound");
        if (asset.CourseId != request.CourseId)
            return Error.Forbidden("CourseAsset.WrongCourse", "Asset does not belong to this course.");

        var result = asset.Archive();
        if (result.IsError) return result.Errors;

        _assetRepository.Update(asset);
        return Result.Success;
    }
}
