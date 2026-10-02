using AlphaZero.Modules.VideoUploading.Domain.Events;
using AlphaZero.Shared.Domain;
using ErrorOr;
using OpenTelemetry.Trace;

namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public class Video : AggregateRoot, IDomainTenantOwned, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public VideoStatus Status { get; private set; }
    public VideoMetadata Metadata { get; private set; } = null!;
    public VideoSpecifications Specifications { get; private set; } = null!;
    public string? ThumbnailUrl => Status == VideoStatus.Published ? VideoConstants.GetThumbnailOutputKey(Id.ToString(), TenantId.ToString()) : null;
    public bool IsDefaultThumbnail { get; private set; } = true;
    public string? PlaybackUrl  => Status == VideoStatus.Published ? VideoConstants.GetPlaybackUrl(Id.ToString(), TenantId.ToString()) : null;
    public DateTime CreatedOn { get; private set; }
    public DateTime? PublishedOn { get; private set; }
    public bool IsDeleted { get; private set; }
    public string SourceKey => VideoConstants.GetInputVideoSourceKey(videoId: Id.ToString(), tenantId: TenantId.ToString());
    public DateTime? OnDeleted { get; private set; } = null!;
    private Video()
    {
        //EF
    }

    private Video(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        VideoMetadata metadata,
        DateTime createdOn, 
        bool isDefaultThumbnail = true) : base(id)
    {
        TenantId = tenantId;
        Title = title;
        Description = description;
        Metadata = metadata;
        Specifications = VideoSpecifications.Empty;
        Status = VideoStatus.Processing;
        CreatedOn = createdOn;
        IsDefaultThumbnail = isDefaultThumbnail;
    }

    public static ErrorOr<Video> Create(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        VideoMetadata metadata,
        DateTime createdOn,
        bool isDefaultThumbnail = true)
    {
        if (string.IsNullOrWhiteSpace(title))
            return VideoErrors.EmptyTitle;

        return new Video(id, tenantId, title, description, metadata, createdOn, isDefaultThumbnail);
    }

    public ErrorOr<Success> MarkAsPublished(DateTime publishOn)
    {
        
        if (Status != VideoStatus.Processing)
            return VideoErrors.InvalidStatus;
        if (Specifications == VideoSpecifications.Empty ||
            (string.IsNullOrEmpty(Metadata.OriginalFileName) && Metadata.FileSize == 0))
            return Error.Forbidden("Video.Publishing", "Can't Pubilsh a Video with empty Specification and metadata");
        Status = VideoStatus.Published;
        PublishedOn = publishOn;

        AddDomainEvent(new VideoPublishedDomainEvent(Id, TenantId,PublishedOn.Value));

        return Result.Success;
    }

    public void MarkAsFailed(string reason)
    {
        Status = VideoStatus.Failed;
        AddDomainEvent(new VideoFailedDomainEvent(Id,TenantId, reason));
    }

    public void MarkAsDeleted()
    {
        Status = VideoStatus.Deleted;
        IsDeleted = true;
    }

    public void UpdateMetadata(VideoMetadata metadata)
    {
        Metadata = metadata;
    }

    public void UpdateSpecifications(VideoSpecifications specifications)
    {
        Specifications = specifications;
    }

    public ErrorOr<Success> UpdateInformation(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            return VideoErrors.EmptyTitle;

        Title = title;
        Description = description;

        AddDomainEvent(new VideoMetadataUpdatedDomainEvent(Id, Title, Description));

        return Result.Success;
    }

    public ErrorOr<Success> SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return VideoErrors.EmptyTitle;

        Title = title;
        return Result.Success;
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }
}
public sealed class S3Uri : IEquatable<S3Uri>
{
    public string Value { get; }
    public string Bucket { get; }
    public string Key { get; }

    public string Prefix =>
        string.IsNullOrEmpty(Key) || !Key.Contains('/')
            ? string.Empty
            : Key[..(Key.LastIndexOf('/') + 1)];

    private S3Uri(string value, string bucket, string key)
    {
        Value = value;
        Bucket = bucket;
        Key = key;
    }

    public static S3Uri Parse(string s3Uri)
    {
        if (string.IsNullOrWhiteSpace(s3Uri))
            throw new ArgumentException("S3 URI cannot be null or empty.", nameof(s3Uri));

        if (!s3Uri.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid S3 URI: {s3Uri}");

        var uri = new Uri(s3Uri);

        var bucket = uri.Host;
        var key = uri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("S3 URI must contain a bucket.");

        return new S3Uri(s3Uri, bucket, key);
    }

    public override string ToString() => Value;

    public bool Equals(S3Uri? other)
    {
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as S3Uri);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(S3Uri? left, S3Uri? right) =>
        Equals(left, right);

    public static bool operator !=(S3Uri? left, S3Uri? right) =>
        !Equals(left, right);
}


public class VideoConstants
{
    public static readonly string[] AllowedVideoFormats = [
    ".mp4"];

    public static readonly string[] AllowedMIMETypes = [  
    "video/mp4",
    ];


    public static readonly string[] AllowedThumbnailExtensions = [
        ".jpg",
        ".jpeg",
        ".webp"
    ];
    
    public static readonly string[] AllowedThumbnailMIMETypes = [  
        "image/jpeg",
        "image/jpg",
        "image/webp"
    ];
 
    public static string GetInputVideoSourceKey(string videoId, string tenantId)
    {
        return $"{tenantId}/{videoId}/source.mp4";
    }

    public static string GetInputS3Url(string bucketName,string videoId, string tenantId)
    {
        return $"s3://{bucketName}/{GetInputVideoSourceKey(videoId,tenantId)}";
    }

    public static string GetThumbnailInputVideoSourceKey(string videoId, string tenantId, string imageExtension)
    {
        return $"{tenantId}/{videoId}/thumbnail{imageExtension}";
    } 

    public static string GetOutputVideoKey(string videoId, string tenantId)
    {
        return $"streaming/{tenantId}/{videoId}/master";
    }

    public static string GetOutputPathS3Url(string bucketName,string videoId, string tenantId)
    {
        return $"s3://{bucketName}/{GetOutputVideoKey(videoId,tenantId)}";
    }

    public static string GetPlaybackUrl(string videoId, string tenantId)
    {
        return $"{tenantId}/{videoId}/master.m3u8";
    }

    public static string GetThumbnailOutputKey(string videoId, string tenantId)
    {
        return $"{tenantId}/{videoId}/thumbnail.jpeg";
    }
} 