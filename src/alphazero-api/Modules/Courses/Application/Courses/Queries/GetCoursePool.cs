using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries;

public record GetCoursePoolQuery(Guid CourseId, bool IncludeAssigned = false) : IQuery<List<CourseAssetDto>>;

public record CourseAssetDto(
    Guid Id,
    string Title,
    string AssetType,       // "Video", "Document", "Assessment"
    string State,           // "Pending", "Available", "Failed", "InUse", "Archived"
    string ResourceArn,
    DateTime UploadedAt,
    TimeSpan? Duration = null,
    string? ThumbnailUrl = null,
    string? FileName = null,
    int? QuestionsNumber = null
);

public sealed class GetCoursePoolQueryHandler 
    : IRequestHandler<GetCoursePoolQuery, ErrorOr<List<CourseAssetDto>>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly ICourseRepository _courseRepository;

    public GetCoursePoolQueryHandler(
        IRepository<CourseAsset> assetRepository,
        ICourseRepository courseRepository)
    {
        _assetRepository = assetRepository;
        _courseRepository = courseRepository;
    }

    public async Task<ErrorOr<List<CourseAssetDto>>> Handle(
        GetCoursePoolQuery request, CancellationToken ct)
    {
        var assets = await _assetRepository.Get(
            a => a.CourseId == request.CourseId && a.State == CourseAssetState.Available, 
            ct);

        if (!request.IncludeAssigned)
        {
            var unassignedAssets = new List<CourseAsset>();
            foreach (var asset in assets)
            {
                if (!await _courseRepository.IsAssetInUseAsync(asset.Id, ct))
                {
                    unassignedAssets.Add(asset);
                }
            }
            assets = unassignedAssets;
        }

        var poolAssets = assets
            .OrderBy(a => a.UploadedUtcAt)
            .Select(Map)
            .ToList();

        return poolAssets;
    }

    private static CourseAssetDto Map(CourseAsset asset)
    {
        var dto = new CourseAssetDto(
            asset.Id,
            asset.Title,
            asset.CourseAssetType.ToString(),
            asset.State.ToString(),
            asset.ResourceArn.Value,
            asset.UploadedUtcAt);

        return asset switch
        {
            VideoCourseAsset video => dto with
            {
                Duration = video.Duration,
                ThumbnailUrl = video.ThumbnailUrl
            },

            DocumentCourseAsset document => dto with
            {
                FileName = document.FileName
            },

            AssessmentCourseAsset assessment => dto with
            {
                QuestionsNumber = assessment.QuestionsNumber
            },

            _ => throw new ArgumentOutOfRangeException(nameof(asset))
        };
    }
}
