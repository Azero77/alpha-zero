using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries;

public record GetCoursePoolQuery(Guid CourseId) : IQuery<List<CourseAssetDto>>;

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

    public GetCoursePoolQueryHandler(IRepository<CourseAsset> assetRepository)
        => _assetRepository = assetRepository;

    public async Task<ErrorOr<List<CourseAssetDto>>> Handle(
        GetCoursePoolQuery request, CancellationToken ct)
    {
        var assets = await _assetRepository.Get(
            a => a.CourseId == request.CourseId && a.State == CourseAssetState.Available, 
            ct);

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
