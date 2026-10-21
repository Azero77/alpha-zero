using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;

public record CourseViewPlanDto(Guid Id, string Name);

/// <summary>
/// A curriculum resource with its real identity (the curriculum placement id, the pool
/// asset id, and the true resource ARN sourced from the pool asset). The legacy
/// always-empty metadata bucket is deliberately NOT carried into this shape (#32).
/// </summary>
public record CourseViewResourceDto(
    Guid CurriculumResourceId,
    string? CourseAssetId,
    string? ResourceArn,
    string Type,
    int Order,
    double DurationSeconds);

public record CourseViewItemDto(
    Guid Id,
    string Title,
    string Type,
    int Order,
    int BitIndex,
    bool IsCompleted,
    List<CourseViewResourceDto> Resources);

public record CourseViewSectionDto(
    Guid Id,
    string Title,
    int Order,
    List<CourseViewItemDto> Items);

/// <summary>
/// The enrollment overlay: only ever resolved for the CALLER's own enrollment
/// (scoped to the caller — never a request-supplied student id, #32). Completed items
/// are item ids overlaid from the enrollment's progress bitmask, filtered by the course's
/// active-items bitmask so deleted curriculum positions never appear completed.
/// </summary>
public record CourseViewEnrollmentDto(
    Guid EnrollmentId,
    string Status,
    double CompletionPercentage,
    List<Guid> CompletedItemIds);

/// <summary>
/// One-row enrollment lookup result for the view overlay (student × course scoped);
/// carries the completed bit indexes so callers can map them onto the curriculum tree.
/// </summary>
public record EnrollmentCourseViewDto(
    Guid EnrollmentId,
    string Status,
    double CompletionPercentage,
    IReadOnlyCollection<int> CompletedBitIndexes);

/// <summary>
/// Student-shaped course detail view (#32): course summary fields (subject NAME — joined,
/// not the raw id — cover image, timestamps) + curriculum tree (sections/items/resource
/// identity without edit-sensitive admin data) + minimal plan summary + optional
/// enrollment overlay. Anonymous/unenrolled callers get the same shape with no overlay.
/// </summary>
public record CourseViewDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    Guid SubjectId,
    string SubjectName,
    Guid? CoverImageDocumentId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int SectionCount,
    int ItemCount,
    double TotalDurationSeconds,
    List<CourseViewPlanDto> Plans,
    List<CourseViewSectionDto> Sections,
    CourseViewEnrollmentDto? Enrollment);

/// <summary>
/// Server-translated course shape for the view (no enrollment concerns): summary fields,
/// curriculum tree with real resource identity (true ARN sourced from the pool asset —
/// the legacy empty metadata bucket is dropped from this shape), plan summary, counts.
/// </summary>
public record CourseSummaryViewDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    Guid SubjectId,
    string SubjectName,
    Guid? CoverImageDocumentId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int SectionCount,
    int ItemCount,
    double TotalDurationSeconds,
    List<CourseViewPlanDto> Plans,
    List<CourseViewSectionDto> Sections);

/// <summary>
/// Student course-detail view query. <c>CallerStudentId</c> is resolved from the CALLER's
/// authenticated identity by the endpoint (empty for anonymous callers); the enrollment
/// overlay is resolved by a single enrollment-service query scoped to caller × course.
/// Pre-request visibility mirrors the GetCourseOverview family (draft content masked).
/// </summary>
public record GetCourseViewQuery(Guid CourseId, Guid CallerStudentId) : IRequest<ErrorOr<CourseViewDto>>;

public sealed class GetCourseViewQueryValidator : AbstractValidator<GetCourseViewQuery>
{
    public GetCourseViewQueryValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class GetCourseViewQueryHandler : IRequestHandler<GetCourseViewQuery, ErrorOr<CourseViewDto>>
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly IEnrollmentQueryService _enrollmentQueryService;

    public GetCourseViewQueryHandler(
        ICourseQueryService courseQueryService,
        IEnrollmentQueryService enrollmentQueryService)
    {
        _courseQueryService = courseQueryService;
        _enrollmentQueryService = enrollmentQueryService;
    }

    public async Task<ErrorOr<CourseViewDto>> Handle(GetCourseViewQuery request, CancellationToken cancellationToken)
    {
        var summary = await _courseQueryService.GetCourseSummaryForViewAsync(request.CourseId, cancellationToken);
        if (summary is null)
        {
            return Error.NotFound("Course.NotFound", "Course not found.");
        }

        var (enrollmentOverlay, overlaidSections) = await ResolveOwnEnrollmentOverlayAsync(summary, request.CallerStudentId, cancellationToken);

        return new CourseViewDto(
            summary.Id,
            summary.Title,
            summary.Description,
            summary.Status,
            summary.SubjectId,
            summary.SubjectName,
            summary.CoverImageDocumentId,
            summary.CreatedAtUtc,
            summary.UpdatedAtUtc,
            summary.SectionCount,
            summary.ItemCount,
            summary.TotalDurationSeconds,
            summary.Plans,
            overlaidSections,
            enrollmentOverlay);
    }

    private async Task<(CourseViewEnrollmentDto? Enrollment, List<CourseViewSectionDto> Sections)> ResolveOwnEnrollmentOverlayAsync(
        CourseSummaryViewDto summary, Guid callerStudentId, CancellationToken cancellationToken)
    {
        if (callerStudentId == Guid.Empty)
        {
            return (null, summary.Sections); // anonymous prospect: public shape, no overlay
        }

        var enrollment = await _enrollmentQueryService.GetEnrollmentForCourseAsync(
            callerStudentId, summary.Id, cancellationToken);
        if (enrollment is null)
        {
            return (null, summary.Sections); // admins/unenrolled callers: same shape, no overlay (#32)
        }

        var completedBitIndexes = enrollment.CompletedBitIndexes.ToHashSet();
        var completedItemIds = summary.Sections
            .SelectMany(s => s.Items)
            .Where(i => completedBitIndexes.Contains(i.BitIndex))
            .Select(i => i.Id)
            .ToList();

        // Per-item IsCompleted (D8): the same bit → item mapping that produced
        // CompletedItemIds drives the flag — one source of truth, no client re-derivation.
        var overlaidSections = summary.Sections
            .Select(s => s with
            {
                Items = s.Items
                    .Select(i => i with { IsCompleted = completedBitIndexes.Contains(i.BitIndex) })
                    .ToList()
            })
            .ToList();

        return (new CourseViewEnrollmentDto(
            enrollment.EnrollmentId,
            enrollment.Status,
            enrollment.CompletionPercentage,
            completedItemIds), overlaidSections);
    }
}
