using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

public class CourseQueryService : ICourseQueryService
{
    private static readonly JsonElement EmptyJsonElement = JsonDocument.Parse("{}").RootElement.Clone();
    private readonly AppDbContext _context;

    public CourseQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Course>()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(c => c.Id == courseId)
            .Select(course => new CourseDto(
                course.Id,
                course.Title,
                course.Description,
                course.SubjectId,
                course.Status.ToString(),
                course.Sections.OrderBy(s => s.Order).Select(s => new SectionDto(
                    s.Id,
                    s.Title,
                    s.Order,
                    s.Items.OrderBy(i => i.Order).Select(i => new ItemDto(
                        i.Id,
                        i.Title,
                        i.MainType,
                        i.Order,
                        i.BitIndex,
                        i.Resources.OrderBy(r => r.Order).Select(r => new ResourceDto(
                            r.Asset != null ? r.Asset.ResourceArn.Value : (r.CourseAssetId != null ? r.CourseAssetId.Value.ToString() : ""),
                            r.ResourceType,
                            r.Order,
                            EmptyJsonElement)).ToList())).ToList())).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<CourseSummaryDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Course>().AsNoTracking();

        if (subjectId.HasValue)
        {
            query = query.Where(c => c.SubjectId == subjectId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Title)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(c => new CourseSummaryDto(
                c.Id,
                c.Title,
                c.Description,
                c.SubjectId,
                c.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedResult<CourseSummaryDto>(items, totalCount, page, perPage);
    }

    public async Task<CourseReadiness?> GetCourseReadinessAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var courseData = await _context.Set<Course>()
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Description,
                c.SubjectId,
                HasSections = c.Sections.Any(s => !s.IsDeleted),
                HasItems = c.Sections.Where(s => !s.IsDeleted).Any(s => s.Items.Any(i => !i.IsDeleted)),
                HasActivePricingPlan = c.Plans.Any(),
                AttachedAssets = c.Sections.Where(s => !s.IsDeleted)
                    .SelectMany(s => s.Items.Where(i => !i.IsDeleted))
                    .SelectMany(i => i.Resources)
                    .Where(r => r.CourseAssetId != null)
                    .Select(r => new
                    {
                        AssetId = r.CourseAssetId!.Value,
                        Title = r.Asset != null ? r.Asset.Title : "",
                        State = r.Asset != null ? r.Asset.State : CourseAssetState.Pending
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (courseData is null)
            return null;

        var overviewData = await _context.Set<CourseOverview>()
            .AsNoTracking()
            .Where(o => o.CourseId == courseId)
            .Select(o => new
            {
                HasDescription = o.DescriptionContent != null && !string.IsNullOrWhiteSpace(o.DescriptionContent.Value) && o.DescriptionContent.Value != "{}",
                HasCoverImage = o.CoverImageDocumentId.HasValue && o.CoverImageDocumentId.Value != Guid.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        var hasTitle = !string.IsNullOrWhiteSpace(courseData.Title);
        var hasSubject = courseData.SubjectId != Guid.Empty;
        var hasDescription = !string.IsNullOrWhiteSpace(courseData.Description) || (overviewData?.HasDescription == true);
        var hasCoverImage = overviewData?.HasCoverImage == true;
        var hasSections = courseData.HasSections;
        var hasItems = courseData.HasItems;
        var hasActivePricingPlan = courseData.HasActivePricingPlan;

        var blockingReasons = new List<string>();
        if (!hasTitle) blockingReasons.Add("Course title is required.");
        if (!hasSubject) blockingReasons.Add("Course subject is required.");
        if (!hasDescription) blockingReasons.Add("Course description is required.");
        if (!hasCoverImage) blockingReasons.Add("Course cover image is required.");
        if (!hasSections) blockingReasons.Add("Course must have at least one section.");
        if (!hasItems) blockingReasons.Add("Course curriculum must have at least one item.");

        var distinctAttached = courseData.AttachedAssets
            .GroupBy(a => a.AssetId)
            .Select(g => g.First())
            .ToList();

        var unreadyAssets = distinctAttached
            .Where(a => a.State != CourseAssetState.Available && a.State != CourseAssetState.InUse)
            .ToList();
        var allAttachedAssetsReady = unreadyAssets.Count == 0;

        foreach (var unready in unreadyAssets)
        {
            if (unready.State == CourseAssetState.Pending)
                blockingReasons.Add($"Video asset '{unready.Title}' is still transcoding.");
            else
                blockingReasons.Add($"Asset '{unready.Title}' is in {unready.State} state.");
        }

        if (!hasActivePricingPlan) blockingReasons.Add("Course must have at least one active pricing plan.");

        var canSubmitForReview = hasTitle && hasSubject && hasDescription && hasSections && hasItems && allAttachedAssetsReady;
        var canPublish = canSubmitForReview && hasCoverImage && hasActivePricingPlan;

        return new CourseReadiness(
            canPublish,
            canSubmitForReview,
            new CourseReadinessChecks(
                new OverviewReadinessChecks(hasTitle, hasSubject, hasDescription, hasCoverImage),
                new CurriculumReadinessChecks(hasSections, hasItems, allAttachedAssetsReady),
                new CommercialReadinessChecks(hasActivePricingPlan)),
            blockingReasons);
    }
}
