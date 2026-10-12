using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class Course : TenantOwnedAggregate, ISoftDeletable
{
    public string Title { get; private set; } = null!;
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
        var course = new Course(id, tenantId, title, description, subjectId);
        course.AddDomainEvent(new CourseCreatedDomainEvent(id, tenantId));
        return course;
    }

    public ErrorOr<Success> AddSection(Guid id,string title)
    {
        var section = CourseSection.Create(id,title, _sections.Count, this.Id, null);
        if (section.IsError)
            return section.Errors;
        _sections.Add(section.Value);
        return Result.Success;
    }

    public int AllocateNextBitIndex() => NextAvailableBitIndex++;

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

    // ─── 2. Asset State Transitions & Pool Operations ───

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

    public ErrorOr<CourseAsset> AssignAsset(Guid assetId)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", $"Asset '{assetId}' not found in course pool.");

        var assignResult = asset.AssignToItem();
        if (assignResult.IsError)
            return assignResult.Errors;

        return asset;
    }

    public ErrorOr<Success> ReleaseAsset(Guid assetId)
    {
        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
            return Error.NotFound("CourseAsset.NotFound", $"Asset '{assetId}' not found in course pool.");

        return asset.ReleaseFromItem();
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

    public CourseReadiness EvaluateReadiness(CourseOverview? overview = null)
    {
        return new CourseReadinessService().Evaluate(this, overview);
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

    internal ErrorOr<Success> Publish()
    {
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
