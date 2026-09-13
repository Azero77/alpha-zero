using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using System.Text.Json;

public abstract class CourseAsset : TenantOwnedEntity
{

    private CourseAsset(Guid id,Guid tenantId,ResourceArn resourceArn, string title, Guid courseId, DateTime uploadedUtcAt, CourseAssetState state)
    : base(id, tenantId)
    {
        ResourceArn = resourceArn;
        Title = title;
        CourseId = courseId;
        UploadedUtcAt = uploadedUtcAt;
        State  = state;
    }
    public abstract CourseAssetType CourseAssetType {get;}
    public ResourceArn ResourceArn {get;private set;}
    public string Title { get;private  set; }
    public Guid CourseId {get;private set;}
    public CourseAssetState State {get;private set;}
    public DateTime UploadedUtcAt {get;private set;}
}

public enum CourseAssetState
{
    Pending,
    Available,
    Archived,
    InUse,
    Failed
}
public enum CourseAssetType
{
    Video, 
    Document, 
    Assessment    
}


public class VideoCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Video;
    public TimeSpan Duration {get;private set;}
    public string? thumbnailUrl {get;private set;}

    public static ErrorOr<VideoCourseAsset> Create(){}


}

public class DocumentCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Document;
    
    public string FileName { get; private set; } = null!;

    public long Size { get; private set; }

    public string ContentType { get; private set; } = null!;

    public static ErrorOr<DocumentCourseAsset> Create(){}
}

public class AssessmentCourseAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Assessment;
    public int QuestionsNumber {get;private set;}
    public AssessmentType Type {get;private set;}

    public static ErrorOr<AssessmentCourseAsset> Create(){}
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
