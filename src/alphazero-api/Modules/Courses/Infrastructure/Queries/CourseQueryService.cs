using AlphaZero.Modules.Courses.Application.Courses.Queries;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourseCatalog;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
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
                        i.MainResourceType.ToString(),
                        i.Order,
                        i.BitIndex,
                        i.Resources.OrderBy(r => r.Order).Select(r => new ResourceDto(
                            r.CourseAssetId != null ? r.CourseAssetId.Value.ToString() : string.Empty,
                            r.ResourceType.ToString(),
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

    /// <summary>
    /// Student catalog (#32). Permission-light and content-safe: the Published status is a
    /// hard server-side constraint (students and anonymous prospects only ever see published
    /// courses; there is no status parameter on this surface). Tenant scope and soft-delete
    /// exclusion come free from the module's global query filters.
    /// Search keywords are LITERAL — <see cref="EscapeLikePattern"/> masks the ILIKE wildcards
    /// (\, %, _) so "Search=%" can no longer match the whole catalog (#32, L3/D10). Postgres
    /// ILIKE's default escape character is the backslash.
    /// Enrollment summary/duration enrichment below mirrors <see cref="GetCourseSummaryForViewAsync"/>
    /// — the correlated subqueries (subject name, overview cover/timestamps, video-duration
    /// sum) are EF-translated inline and shared only as a documented keep-in-sync contract.
    /// </summary>
    public async Task<PagedResult<CourseCatalogItemDto>> ListCourseCatalogAsync(
        string? search, Guid? subjectId, string? sort, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Course>().AsNoTracking()
            .Where(c => c.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = EscapeLikePattern(search.Trim());
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, $"%{keyword}%", @"\") ||
                (c.Description != null && EF.Functions.ILike(c.Description, $"%{keyword}%", @"\")));
        }

        if (subjectId.HasValue)
        {
            query = query.Where(c => c.SubjectId == subjectId.Value);
        }

        var sortKey = string.IsNullOrWhiteSpace(sort) ? CatalogSortKeys.Newest : sort.Trim().ToLowerInvariant();
        var ordered = sortKey switch
        {
            CatalogSortKeys.Title => query.OrderBy(c => c.Title).ThenBy(c => c.Id),
            _ => query.OrderByDescending(c => _context.Set<CourseOverview>()
                        .Where(o => o.CourseId == c.Id)
                        .Select(o => o.CreatedAtUtc)
                        .FirstOrDefault())
                    .ThenBy(c => c.Id)
        };

        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .AsSplitQuery()
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(c => new CourseCatalogItemDto(
                c.Id,
                c.Title,
                c.Description,
                c.Status.ToString(),
                c.SubjectId,
                _context.Set<Subject>().Where(s => s.Id == c.SubjectId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                _context.Set<CourseOverview>().Where(o => o.CourseId == c.Id).Select(o => o.CoverImageDocumentId).FirstOrDefault(),
                c.Sections.Count,
                c.Sections.SelectMany(s => s.Items).Count(),
                c.Sections.SelectMany(s => s.Items)
                    .SelectMany(i => i.Resources)
                    .SelectMany(r => _context.Set<VideoCourseAsset>()
                        .Where(a => a.Id == r.CourseAssetId)
                        .Select(a => a.Duration.TotalSeconds))
                    .Sum(),
                c.Plans.Select(p => new CourseCatalogPlanSummaryDto(p.Id, p.Name)).ToList(),
                _context.Set<CourseOverview>().Where(o => o.CourseId == c.Id).Select(o => o.CreatedAtUtc).FirstOrDefault(),
                _context.Set<CourseOverview>().Where(o => o.CourseId == c.Id).Select(o => o.UpdatedAtUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<CourseCatalogItemDto>(items, totalCount, page, perPage);
    }

    /// <summary>
    /// Escapes the ILIKE wildcards (\, %, _) in a user-supplied keyword so the pattern the
    /// keyword participates in treats every character literally (#32, L3/D10). The backslash
    /// must be escaped FIRST. <see cref="EF.Functions.ILike(string,string)"/>'s 2-argument
    /// overload translates with ESCAPE '' (no escape character), so the 3-argument overload
    /// must be used with the explicit "\" escape char — that is what makes \, \%, \_ literal.
    /// </summary>
    private static string EscapeLikePattern(string keyword) => keyword
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_");

    public async Task<string?> GetCourseStatusAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Course>()
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => c.Status.ToString())
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Student course-detail shape (#32): summary fields (subject NAME join, cover image,
    /// timestamps sourced from the overview row, counts, total video duration, plan summary)
    /// plus the curriculum tree with real resource identity — placement id, course-asset id,
    /// and the TRUE resource ARN sourced from the pool asset. The legacy always-empty
    /// metadata bucket is deliberately absent from this shape.
    /// </summary>
    public async Task<CourseSummaryViewDto?> GetCourseSummaryForViewAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Course>()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(c => c.Id == courseId)
            .Select(course => new CourseSummaryViewDto(
                course.Id,
                course.Title,
                course.Description,
                course.Status.ToString(),
                course.SubjectId,
                _context.Set<Subject>().Where(s => s.Id == course.SubjectId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                _context.Set<CourseOverview>().Where(o => o.CourseId == course.Id).Select(o => o.CoverImageDocumentId).FirstOrDefault(),
                _context.Set<CourseOverview>().Where(o => o.CourseId == course.Id).Select(o => o.CreatedAtUtc).FirstOrDefault(),
                _context.Set<CourseOverview>().Where(o => o.CourseId == course.Id).Select(o => o.UpdatedAtUtc).FirstOrDefault(),
                course.Sections.Count,
                course.Sections.SelectMany(s => s.Items).Count(),
                course.Sections.SelectMany(s => s.Items)
                    .SelectMany(i => i.Resources)
                    .SelectMany(r => _context.Set<VideoCourseAsset>()
                        .Where(a => a.Id == r.CourseAssetId)
                        .Select(a => a.Duration.TotalSeconds))
                    .Sum(),
                course.Plans.Select(p => new CourseViewPlanDto(p.Id, p.Name)).ToList(),
                course.Sections.OrderBy(s => s.Order).Select(s => new CourseViewSectionDto(
                    s.Id,
                    s.Title,
                    s.Order,
                    s.Items.OrderBy(i => i.Order).Select(i => new CourseViewItemDto(
                        i.Id,
                        i.Title,
                        i.MainResourceType.ToString(),
                        i.Order,
                        i.BitIndex,
                        false, // no enrollment concerns in the base projection — the overlay sets the flag when resolving
                        i.Resources.OrderBy(r => r.Order).Select(r => new CourseViewResourceDto(
                            r.Id,
                            r.CourseAssetId == null ? null : r.CourseAssetId.Value.ToString(),
                            _context.Set<CourseAsset>().Where(a => a.Id == r.CourseAssetId).Select(a => a.ResourceArn.Value).FirstOrDefault(),
                            r.ResourceType.ToString(),
                            r.Order,
                            _context.Set<VideoCourseAsset>().Where(a => a.Id == r.CourseAssetId).Select(a => (double?)a.Duration.TotalSeconds).FirstOrDefault() ?? 0d)).ToList())).ToList())).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Server-translated SQL projection of <c>CourseReadinessService.Evaluate</c> (Domain/Services),
    /// which is the readiness source of truth for the publish path. This projection exists so the
    /// readiness endpoint does not have to load the aggregate graph. Keep the checks and blocking
    /// reasons here in sync with <c>CourseReadinessService</c>.
    /// </summary>
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
                AssetIds = c.Sections.Where(s => !s.IsDeleted)
                    .SelectMany(s => s.Items.Where(i => !i.IsDeleted))
                    .SelectMany(i => i.Resources)
                    .Where(r => r.CourseAssetId != null)
                    .Select(r => r.CourseAssetId!.Value)
                    .Distinct()
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

        var assetData = await _context.Set<CourseAsset>()
            .AsNoTracking()
            .Where(a => courseData.AssetIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Title, a.State })
            .ToListAsync(cancellationToken);

        var foundAssetIds = assetData.Select(a => a.Id).ToHashSet();
        var missingAssetIds = courseData.AssetIds.Where(id => !foundAssetIds.Contains(id)).ToList();
        if (missingAssetIds.Any())
        {
            blockingReasons.Add($"Course references {missingAssetIds.Count} asset(s) that could not be found.");
        }

        var unreadyAssets = assetData
            .Where(a => a.State != CourseAssetState.Available)
            .ToList();
        var allAttachedAssetsReady = unreadyAssets.Count == 0 && missingAssetIds.Count == 0;

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

    public async Task<CourseOverviewDto?> GetCourseOverviewAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var query = from course in _context.Courses.AsNoTracking()
                    join courseOverview in _context.CourseOverviews.AsNoTracking()
                        on course.Id equals courseOverview.CourseId
                    where course.Id == courseId
                    select new CourseOverviewDto(
                        courseOverview.Id,
                        course.Id,
                        course.Status.ToString(),
                        courseOverview.DescriptionContent.Value,
                        courseOverview.TargetAudienceContent == null ? null : courseOverview.TargetAudienceContent.Value,
                        courseOverview.LearningObjectivesContent == null ? null : courseOverview.LearningObjectivesContent.Value,
                        courseOverview.CoverImageDocumentId,
                        courseOverview.ETag);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<CourseAssetDto>> GetCoursePoolAsync(
        Guid courseId, 
        bool includeAssigned = false, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.CourseAssets
            .AsNoTracking()
            .Where(a => a.CourseId == courseId && a.State == CourseAssetState.Available);

        if (!includeAssigned)
        {
            var assignedAssetIds = _context.CurriculumResources
                .AsNoTracking()
                .Where(r => r.CourseAssetId != null)
                .Select(r => r.CourseAssetId!.Value);

            query = query.Where(a => !assignedAssetIds.Contains(a.Id));
        }

        var assets = await query
            .OrderBy(a => a.UploadedUtcAt)
            .ToListAsync(cancellationToken);

        return assets.Select(MapCourseAssetToDto).ToList();
    }

    private static CourseAssetDto MapCourseAssetToDto(CourseAsset asset)
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
            DocumentCourseAsset doc => dto with
            {
                FileName = doc.FileName
            },
            AssessmentCourseAsset assessment => dto with
            {
                QuestionsNumber = assessment.QuestionsNumber
            },
            _ => dto
        };
    }
}
