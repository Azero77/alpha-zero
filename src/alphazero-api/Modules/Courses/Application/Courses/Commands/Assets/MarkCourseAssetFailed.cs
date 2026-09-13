using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record MarkCourseAssetFailedCommand(Guid AssetId, string Reason) : ICommand<Success>;

public sealed class MarkCourseAssetFailedCommandHandler 
    : IRequestHandler<MarkCourseAssetFailedCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly ILogger<MarkCourseAssetFailedCommandHandler> _logger;

    public MarkCourseAssetFailedCommandHandler(
        IRepository<CourseAsset> assetRepository,
        ILogger<MarkCourseAssetFailedCommandHandler> logger)
    {
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(
        MarkCourseAssetFailedCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.AssetId, ct);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Course asset not found.");

        var result = asset.MarkFailed();
        if (result.IsError) return result.Errors;

        _assetRepository.Update(asset);
        _logger.LogWarning("CourseAsset {AssetId} marked as failed. Reason: {Reason}", 
            request.AssetId, request.Reason);

        return Result.Success;
    }
}
