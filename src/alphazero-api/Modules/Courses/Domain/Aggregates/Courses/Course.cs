using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class Course : TenantOwnedAggregate, ISoftDeletable
{
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Guid SubjectId { get; private set; }
    public CourseStatus Status { get; private set; }
    public int NextAvailableBitIndex { get; private set; } 

    public IReadOnlyCollection<CourseSection> Sections => _sections.AsReadOnly();
    private readonly List<CourseSection> _sections = new();
    
    public IReadOnlyCollection<CoursePlan> Plans => _plans.AsReadOnly();
    private readonly List<CoursePlan> _plans = new();

    private readonly List<CourseAsset> _assets = new();
    public IReadOnlyCollection<CourseAsset> Assets => _assets.AsReadOnly();

    public IEnumerable<CourseAsset> PoolAssets => _assets.Where(a => a.State == CourseAssetState.Available);
    public IEnumerable<CourseAsset> PendingAssets => _assets.Where(a => a.State == CourseAssetState.Pending);

    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; }

    private Course() : base(default, default) { } // EF Core

    private Course(Guid id, Guid tenantId, string title, string? description, Guid subjectId) : base(id, tenantId)
    {
        Title = title;
        Description = description;
        SubjectId = subjectId;
        Status = CourseStatus.Draft;
        NextAvailableBitIndex = 0;
    }

    public static ErrorOr<Course> Create(Guid id, Guid tenantId, string title, string? description, Guid subjectId)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("Course.Title", "Title is required.");
        return new Course(id, tenantId, title, description, subjectId);
    }

    public void AddSection(string title)
    {
        var section = CourseSection.Create(TenantId, title, _sections.Count, this.Id);
        _sections.Add(section);
    }

    public ErrorOr<CoursePlan> AddPlan(string name, Guid principalId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Course.PlanName", "Plan name is required.");
        
        if (_plans.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Error.Conflict("Course.PlanExists", $"A plan with name '{name}' already exists.");

        var plan = CoursePlan.Create(Id, name, principalId);
        _plans.Add(plan);
        return plan;
    }

    public ErrorOr<Success> UpdatePlan(Guid planId, string name, Guid principalId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Course.PlanName", "Plan name is required.");

        var plan = _plans.FirstOrDefault(p => p.Id == planId);
        if (plan == null) return Error.NotFound("Course.Plan", "Plan not found.");

        if (_plans.Any(p => p.Id != planId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Error.Conflict("Course.PlanExists", $"A plan with name '{name}' already exists.");

        plan.Update(name, principalId);
        return Result.Success;
    }

    public ErrorOr<Success> RemovePlan(Guid planId)
    {
        var plan = _plans.FirstOrDefault(p => p.Id == planId);
        if (plan == null) return Error.NotFound("Course.Plan", "Plan not found.");

        _plans.Remove(plan);
        return Result.Success;
    }

    // ─── 1. Asset Creation ───

    public ErrorOr<VideoCourseAsset> AddVideoAsset(Guid videoId, ResourceArn videoArn, string title)
    {
        // Idempotency
        if (_assets.Any(a => a.Id == videoId))
            return (VideoCourseAsset)_assets.First(a => a.Id == videoId);

        var result = VideoCourseAsset.Create(videoId, TenantId, videoArn, title, Id);
        if (result.IsError) return result.Errors;

        _assets.Add(result.Value);
        return result.Value;
    }

    public ErrorOr<DocumentCourseAsset> AddDocumentAsset(
        Guid docId, ResourceArn docArn, string title, string fileName, long size, string contentType)
    {
        if (_assets.Any(a => a.Id == docId))
            return (DocumentCourseAsset)_assets.First(a => a.Id == docId);

        var result = DocumentCourseAsset.Create(docId, TenantId, docArn, title, Id, fileName, size, contentType);
        if (result.IsError) return result.Errors;

        _assets.Add(result.Value);
        return result.Value;
    }

    public ErrorOr<AssessmentCourseAsset> AddAssessmentAsset(
        Guid assessmentId, ResourceArn assessmentArn, string title, int questionsNumber, AssessmentType type)
    {
        if (_assets.Any(a => a.Id == assessmentId))
            return (AssessmentCourseAsset)_assets.First(a => a.Id == assessmentId);

        var result = AssessmentCourseAsset.Create(assessmentId, TenantId, assessmentArn, title, Id, questionsNumber, type);
        if (result.IsError) return result.Errors;

        _assets.Add(result.Value);
        return result.Value;
    }

    // ─── 2. Asset State Transitions ───

    public ErrorOr<Success> MarkAssetAvailable(Guid assetId, TimeSpan? duration = null, string? relativeUrl = null, string? thumbnailUrl = null)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Asset not found in this course.");

        if (asset is VideoCourseAsset video && (duration.HasValue || relativeUrl is not null))
            video.UpdateStreamingInfo(duration ?? TimeSpan.Zero, relativeUrl, thumbnailUrl);

        return asset.MarkAvailable();
    }

    public ErrorOr<Success> MarkAssetFailed(Guid assetId)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Asset not found in this course.");

        return asset.MarkFailed();
    }

    // ─── 3. Pool Operations (teacher-facing) ───

    public ErrorOr<CurriculumItem> AssignAssetToCurriculum(Guid assetId, Guid sectionId, string title, JsonElement metadata)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", "Asset not found in this course.");
        if (asset.State != CourseAssetState.Available)
            return Error.Conflict("CourseAsset.NotAvailable",
                $"Asset is in state '{asset.State}', must be Available to assign.");

        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var mainType = asset.CourseAssetType.ToString();
        var item = new CurriculumItem(
            Guid.NewGuid(), TenantId, sectionId, title,
            section.Items.Count, NextAvailableBitIndex++, mainType);

        var addResult = item.AddResource(asset, metadata);
        if (addResult.IsError) return addResult.Errors;

        section.AddItem(item);

        var stateResult = asset.MarkInUse();
        if (stateResult.IsError) return stateResult.Errors;

        return item;
    }

    public ErrorOr<Success> UnassignFromCurriculum(Guid itemId)
    {
        var section = _sections.FirstOrDefault(s => s.Items.Any(i => i.Id == itemId));
        if (section is null)
            return Error.NotFound("Section.NotFound", "No section contains this item.");

        var item = section.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Error.NotFound("CurriculumItem.NotFound");

        foreach (var resource in item.Resources)
        {
            var asset = _assets.FirstOrDefault(a => a.Id == resource.CourseAssetId);
            if (asset is not null && asset.State == CourseAssetState.InUse)
            {
                asset.ReturnToPool();
            }
        }

        item.Delete();
        return Result.Success;
    }

    public ErrorOr<Success> DismissAsset(Guid assetId)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound");

        return asset.Archive();
    }

    public ErrorOr<Success> ReorderItems(Guid sectionId, List<Guid> itemIds)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section == null) return Error.NotFound("Course.Section", "Section not found.");
        for (int i = 0; i < itemIds.Count; i++)
        {
            var item = section.Items.FirstOrDefault(x => x.Id == itemIds[i]);
            item?.UpdateOrder(i);
        }
        return Result.Success;
    }

    public int TotalTrackedItems => NextAvailableBitIndex;
    public int ActiveTrackedItems => _sections
        .Where(s => !s.IsDeleted)
        .SelectMany(s => s.Items)
        .Count(i => !i.IsDeleted);

    public void UpdateInformation(string title, string? description, Guid subjectId)
    {
        Title = title;
        Description = description;
        SubjectId = subjectId;
    }

    public ErrorOr<Success> SubmitForReview()
    {
        if (Status != CourseStatus.Draft) return Error.Conflict("Course.Status", "Only draft courses can be reviewed.");
        if (_sections.Count == 0 || _sections.All(s => s.Items.Count == 0)) 
            return Error.Validation("Course.Empty", "Course must have content before review.");
        Status = CourseStatus.UnderReview;
        return Result.Success;
    }

    public ErrorOr<Success> Approve()
    {
        if (Status != CourseStatus.UnderReview) 
            return Error.Conflict("Course.Status", "Only courses under review can be approved.");
        
        Status = CourseStatus.Approved;
        return Result.Success;
    }

    public ErrorOr<Success> Reject(string reason)
    {
        if (Status != CourseStatus.UnderReview) 
            return Error.Conflict("Course.Status", "Only courses under review can be rejected.");
        if(string.IsNullOrEmpty(reason))
            return Error.Validation("Course.RejectionReason", "Rejection reason is required.");
        Status = CourseStatus.Draft;
        return Result.Success;
    }

    public ErrorOr<Success> Publish()
    {
        if (Status != CourseStatus.Approved) 
            return Error.Conflict("Course.Status", "Only approved courses can be published.");
            
        if (_plans.Count == 0)
            return Error.Validation("Course.NoPlans", "Course must have at least one plan before it can be published.");
        
        Status = CourseStatus.Published;
        AddDomainEvent(new CoursePublishedDomainEvent(Id));
        return Result.Success;
    }

    public ErrorOr<Success> Archive()
    {
        if (Status == CourseStatus.Archived) 
            return Error.Conflict("Course.Status", "Course is already archived.");
        
        Status = CourseStatus.Archived;
        return Result.Success;
    }

    public ErrorOr<Success> ReorderSections(List<Guid> sectionIds)
    {
        if (Status == CourseStatus.Published)
            return Error.Conflict("Course.Status", "Cannot reorder sections once published as it may confuse existing students.");

        for (int i = 0; i < sectionIds.Count; i++)
        {
            var section = _sections.FirstOrDefault(s => s.Id == sectionIds[i]);
            if (section != null)
            {
                section.Update(section.Title, i);
            }
        }
        return Result.Success;
    }
}
