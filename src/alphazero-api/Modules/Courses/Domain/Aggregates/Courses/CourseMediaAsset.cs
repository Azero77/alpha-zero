using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public abstract class CourseAsset : TenantOwnedEntity
{
    // EF Core constructor
    protected CourseAsset() : base(default, default) { }

    protected CourseAsset(
        Guid id, Guid tenantId, ResourceArn resourceArn, 
        string title, Guid courseId, DateTime uploadedUtcAt, CourseAssetState state)
        : base(id, tenantId)
    {
        ResourceArn = resourceArn;
        Title = title;
        CourseId = courseId;
        UploadedUtcAt = uploadedUtcAt;
        State = state;
    }

    public abstract CourseAssetType CourseAssetType { get; }
    public ResourceArn ResourceArn { get; private set; }
    public string Title { get; private set; }
    public Guid CourseId { get; private set; }
    public CourseAssetState State { get; private set; }
    public DateTime UploadedUtcAt { get; private set; }

    // ─── State Transitions ───

    public ErrorOr<Success> MarkAvailable()
    {
        if (State != CourseAssetState.Pending)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot mark asset as available from state '{State}'. Must be Pending.");
        State = CourseAssetState.Available;
        return Result.Success;
    }

    public ErrorOr<Success> MarkFailed()
    {
        if (State != CourseAssetState.Pending)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot mark asset as failed from state '{State}'. Must be Pending.");
        State = CourseAssetState.Failed;
        return Result.Success;
    }

    public ErrorOr<Success> MarkInUse()
    {
        if (State != CourseAssetState.Available)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot mark asset as in-use from state '{State}'. Must be Available.");
        State = CourseAssetState.InUse;
        return Result.Success;
    }

    public ErrorOr<Success> ReturnToPool()
    {
        if (State != CourseAssetState.InUse)
            return Error.Conflict("CourseAsset.State", 
                $"Cannot return asset to pool from state '{State}'. Must be InUse.");
        State = CourseAssetState.Available;
        return Result.Success;
    }

    public ErrorOr<Success> Archive()
    {
        if (State == CourseAssetState.InUse)
            return Error.Conflict("CourseAsset.State", 
                "Cannot archive an asset that is currently in use. Unassign it first.");
        State = CourseAssetState.Archived;
        return Result.Success;
    }

    public void UpdateTitle(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
            Title = title;
    }
}

// ─── Enums ───

public enum CourseAssetState { Pending, Available, Archived, InUse, Failed }
public enum CourseAssetType { Video, Document, Assessment }

// ─── Subtypes ───

public class VideoCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Video;
    public TimeSpan Duration { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? RelativeStreamingUrl { get; private set; }

    private VideoCourseAsset() { } // EF Core

    private VideoCourseAsset(
        Guid id, Guid tenantId, ResourceArn arn, string title, 
        Guid courseId, DateTime uploadedAt)
        : base(id, tenantId, arn, title, courseId, uploadedAt, CourseAssetState.Pending) { }

    public static ErrorOr<VideoCourseAsset> Create(
        Guid id, Guid tenantId, ResourceArn videoArn, string title, Guid courseId)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("VideoCourseAsset.Title", "Title is required.");

        return new VideoCourseAsset(id, tenantId, videoArn, title, courseId, DateTime.UtcNow);
    }

    public void UpdateStreamingInfo(TimeSpan duration, string? relativeUrl, string? thumbnailUrl)
    {
        Duration = duration;
        RelativeStreamingUrl = relativeUrl;
        ThumbnailUrl = thumbnailUrl;
    }
}

public class DocumentCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Document;
    public string FileName { get; private set; } = null!;
    public long Size { get; private set; }
    public string ContentType { get; private set; } = null!;

    private DocumentCourseAsset() { } // EF Core

    private DocumentCourseAsset(
        Guid id, Guid tenantId, ResourceArn arn, string title,
        Guid courseId, string fileName, long size, string contentType, DateTime uploadedAt)
        : base(id, tenantId, arn, title, courseId, uploadedAt, CourseAssetState.Pending)
    {
        FileName = fileName;
        Size = size;
        ContentType = contentType;
    }

    public static ErrorOr<DocumentCourseAsset> Create(
        Guid id, Guid tenantId, ResourceArn docArn, string title,
        Guid courseId, string fileName, long size = 0, string contentType = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("DocumentCourseAsset.Title", "Title is required.");
        if (string.IsNullOrWhiteSpace(fileName))
            return Error.Validation("DocumentCourseAsset.FileName", "File name is required.");

        return new DocumentCourseAsset(id, tenantId, docArn, title, courseId, fileName, size, contentType, DateTime.UtcNow);
    }

    public void UpdateFileInfo(string fileName, long size, string contentType)
    {
        FileName = fileName;
        Size = size;
        ContentType = contentType;
    }
}

public class AssessmentCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Assessment;
    public int QuestionsNumber { get; private set; }
    public AssessmentType Type { get; private set; }

    private AssessmentCourseAsset() { } // EF Core

    private AssessmentCourseAsset(
        Guid id, Guid tenantId, ResourceArn arn, string title,
        Guid courseId, int questionsNumber, AssessmentType type, DateTime uploadedAt)
        : base(id, tenantId, arn, title, courseId, uploadedAt, CourseAssetState.Pending)
    {
        QuestionsNumber = questionsNumber;
        Type = type;
    }

    public static ErrorOr<AssessmentCourseAsset> Create(
        Guid id, Guid tenantId, ResourceArn assessmentArn, string title,
        Guid courseId, int questionsNumber = 0, AssessmentType type = AssessmentType.Practice)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("AssessmentCourseAsset.Title", "Title is required.");

        return new AssessmentCourseAsset(id, tenantId, assessmentArn, title, courseId, questionsNumber, type, DateTime.UtcNow);
    }

    public void UpdateAssessmentInfo(int questionsNumber, AssessmentType type)
    {
        QuestionsNumber = questionsNumber;
        Type = type;
    }
}

public enum AssessmentType
{
    /// <summary>
    /// Used during learning to practice or assess understanding of a lesson or concept.
    /// </summary>
    Practice,

    /// <summary>
    /// Formal assessment conducted during the course to evaluate progress.
    /// </summary>
    Midterm,

    /// <summary>
    /// Formal assessment conducted at the end of the course.
    /// </summary>
    Final
}
