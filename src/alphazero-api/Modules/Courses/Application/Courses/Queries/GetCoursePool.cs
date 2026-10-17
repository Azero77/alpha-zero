using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries;

public record GetCoursePoolQuery(Guid CourseId, bool IncludeAssigned = false) : IQuery<List<CourseAssetDto>>;

public class GetCoursePoolQueryValidator : AbstractValidator<GetCoursePoolQuery>
{
    public GetCoursePoolQueryValidator() => RuleFor(x => x.CourseId).NotEmpty();
}

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
    private readonly ICourseQueryService _courseQueryService;

    public GetCoursePoolQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<List<CourseAssetDto>>> Handle(
        GetCoursePoolQuery request, CancellationToken ct)
    {
        var poolAssets = await _courseQueryService.GetCoursePoolAsync(
            request.CourseId, 
            request.IncludeAssigned, 
            ct);

        return poolAssets;
    }
}
