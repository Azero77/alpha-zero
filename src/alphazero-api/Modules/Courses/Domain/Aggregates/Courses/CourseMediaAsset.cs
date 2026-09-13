using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using System.Text.Json;

public abstract class CourseAsset : TenantOwnedEntity
{

    private CourseAsset(Guid id,Guid tenantId,ResourceArn resourceArn, string title, Guid courseId, DateTime uploadedUtcAt)
    : base(id, tenantId)
    {
        ResourceArn = resourceArn;
        Title = title;
        CourseId = courseId;
        UploadedUtcAt = uploadedUtcAt;
    }
    public abstract CourseAssetType CourseAssetType {get;}
    public ResourceArn ResourceArn {get;private set;}
    public string Title { get;private  set; }
    public Guid CourseId {get;private set;}
    public DateTime UploadedUtcAt {get;private set;}

    

}

public enum CourseMediaStatus
{
    /// <summary>
    /// The file or video hasn't been uploaded yet, 
    /// </summary>
    Pending,
    /// <summary>
    /// The file has benn uploaded but not yet processed, like transcoding for a video, zipping for folder,...
    /// </summary>
    Uploaded,

    /// <summary>
    /// The file is ready to be assigned to the course item
    /// </summary>
    Ready,

    /// <summary>
    /// it is being used inside the course
    /// </summary>
    InUse,

    /// <summary>
    /// Archived, Deleted from the course but yet still can be referenced in the future
    /// </summary>
    Archive
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

}

public class Document : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Document;
    public TimeSpan Duration {get;private set;}
}

public class AssessmentAsset : CourseAsset
{
    public override CourseAssetType CourseAssetType => CourseAssetType.Assessment;
    public int QuestionsNumber {get;private set;}
}

public enum AssessmentType
{
    /// <summary>
    /// Usually in course lesson for testing the learning of a concept
    /// </summary>
    Optional,

    /// <summary>
    /// 
    /// </summary>
    Mid,
    Final
}
