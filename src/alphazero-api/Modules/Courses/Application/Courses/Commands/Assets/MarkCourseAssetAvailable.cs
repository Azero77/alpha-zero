using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record MarkCourseAssetAvailableCommand(
    Guid AssetId,
    JsonElement? Payload = null
) : ICommand<Success>;

public sealed class MarkCourseAssetAvailableCommandHandler 
    : IRequestHandler<MarkCourseAssetAvailableCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly IEnumerable<ICourseAssetReadinessStrategy> _strategies;

    public MarkCourseAssetAvailableCommandHandler(
        IRepository<CourseAsset> assetRepository,
        IEnumerable<ICourseAssetReadinessStrategy> strategies)
    {
        _assetRepository = assetRepository;
        _strategies = strategies;
    }

    public async Task<ErrorOr<Success>> Handle(
        MarkCourseAssetAvailableCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Course asset not found.");

        // Apply type-specific strategy
        var strategy = _strategies.FirstOrDefault(s => s.AssetType == asset.CourseAssetType);
        if (strategy is not null)
        {
            var strategyResult = strategy.ApplyReadiness(asset, request.Payload);
            if (strategyResult.IsError) return strategyResult.Errors;
        }

        var markResult = asset.MarkAvailable();
        if (markResult.IsError) return markResult.Errors;

        _assetRepository.Update(asset);
        return Result.Success;
    }
}
