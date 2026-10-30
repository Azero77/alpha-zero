using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;

/// <summary>
/// Sort keys for the student catalog (#32). "newest" orders by the course's overview
/// creation timestamp (the module's per-course chronological anchor); "title" orders
/// alphabetically. Search keyword matching, the sort key, and paging are plain
/// server-side translations — no client-side computation.
/// </summary>
public static class CatalogSortKeys
{
    public const string Newest = "newest";
    public const string Title = "title";
}

public record CourseCatalogPlanSummaryDto(Guid Id, string Name);

/// <summary>
/// Student-facing catalog summary: rich product shape (cover image, subject NAME — joined,
/// not the raw id — total duration, item counts, minimal plan summary, timestamps).
/// Readiness internals and asset-pool data are never exposed here (#32, story #4).
/// </summary>
public record CourseCatalogItemDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    Guid SubjectId,
    string SubjectName,
    Guid? CoverImageDocumentId,
    int SectionCount,
    int ItemCount,
    double TotalDurationSeconds,
    List<CourseCatalogPlanSummaryDto> Plans,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Student catalog query (#32). The published-status constraint is enforced server-side
/// in the query service — students and prospects only ever see Published courses, so
/// there is deliberately no status parameter on this query.
/// </summary>
public record ListCourseCatalogQuery(
    string? Search = null,
    Guid? SubjectId = null,
    string? Sort = null,
    int Page = 1,
    int PerPage = 10) : IRequest<ErrorOr<PagedResult<CourseCatalogItemDto>>>;

public sealed class ListCourseCatalogQueryValidator : AbstractValidator<ListCourseCatalogQuery>
{
    public ListCourseCatalogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PerPage).GreaterThanOrEqualTo(1).LessThanOrEqualTo(100);
        // Sort keys are matched case-insensitively (#32, L4/D10) — CourseQueryService
        // normalizes the key to lowercase ("title"/"newest") anyway, so "Title" must not 400.
        RuleFor(x => x.Sort)
            .Must(s => s is null || IsValidSortKey(s))
            .WithMessage("Sort must be 'newest' or 'title'.");
    }

    private static bool IsValidSortKey(string raw)
    {
        var key = raw.Trim();
        return key.Equals(CatalogSortKeys.Newest, StringComparison.OrdinalIgnoreCase)
            || key.Equals(CatalogSortKeys.Title, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ListCourseCatalogQueryHandler
    : IRequestHandler<ListCourseCatalogQuery, ErrorOr<PagedResult<CourseCatalogItemDto>>>
{
    private readonly ICourseQueryService _courseQueryService;

    public ListCourseCatalogQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<PagedResult<CourseCatalogItemDto>>> Handle(
        ListCourseCatalogQuery request,
        CancellationToken cancellationToken)
        => await _courseQueryService.ListCourseCatalogAsync(
            request.Search, request.SubjectId, request.Sort, request.Page, request.PerPage, cancellationToken);
}
