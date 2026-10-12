using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

namespace AlphaZero.Modules.Courses.Domain.Services;

public class CourseReadinessService
{
    public CourseReadiness Evaluate(Course course, CourseOverview? overview = null)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(course.Title);
        var hasSubject = course.SubjectId != Guid.Empty;
        var hasDescription = !string.IsNullOrWhiteSpace(course.Description)
            || (overview != null && !string.IsNullOrWhiteSpace(overview.DescriptionContent?.Value) && overview.DescriptionContent?.Value != "{}");
        var hasCoverImage = overview?.CoverImageDocumentId.HasValue == true && overview.CoverImageDocumentId.Value != Guid.Empty;

        var activeSections = course.Sections.Where(s => !s.IsDeleted).ToList();
        var hasSections = activeSections.Count > 0;
        var activeItems = activeSections.SelectMany(s => s.Items.Where(i => !i.IsDeleted)).ToList();
        var hasItems = activeItems.Count > 0;

        var blockingReasons = new List<string>();

        if (!hasTitle) blockingReasons.Add("Course title is required.");
        if (!hasSubject) blockingReasons.Add("Course subject is required.");
        if (!hasDescription) blockingReasons.Add("Course description is required.");
        if (!hasCoverImage) blockingReasons.Add("Course cover image is required.");
        if (!hasSections) blockingReasons.Add("Course must have at least one section.");
        if (!hasItems) blockingReasons.Add("Course curriculum must have at least one item.");

        var attachedAssetIds = activeItems
            .SelectMany(i => i.Resources.Select(r => r.CourseAssetId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var attachedAssets = course.Assets.Where(a => attachedAssetIds.Contains(a.Id)).ToList();
        var unreadyAttachedAssets = attachedAssets
            .Where(a => a.State != CourseAssetState.Available && a.State != CourseAssetState.InUse)
            .ToList();
        var allAttachedAssetsReady = unreadyAttachedAssets.Count == 0;

        foreach (var unready in unreadyAttachedAssets)
        {
            if (unready.State == CourseAssetState.Pending)
                blockingReasons.Add($"Video asset '{unready.Title}' is still transcoding.");
            else
                blockingReasons.Add($"Asset '{unready.Title}' is in {unready.State} state.");
        }

        var hasActivePricingPlan = course.Plans.Count > 0;
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
