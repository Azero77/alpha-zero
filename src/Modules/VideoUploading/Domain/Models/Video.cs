using AlphaZero.Modules.VideoUploading.Domain.Events;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public class Video : AggregateRoot, IDomainTenantOwned, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public VideoStatus Status { get; private set; }
    public VideoMetadata Metadata { get; private set; } = null!;
    public VideoSpecifications Specifications { get; private set; } = null!;
    public ThumbnailInfo Thumbnail { get; private set; } = null!;
    public string SourceKey { get; private set; } = null!;
    public string? OutputFolder { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? PublishedOn { get; private set; }
    public bool IsDeleted { get; private set; }

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
        string sourceKey,
        VideoMetadata metadata,
        ThumbnailInfo thumbnail,
        DateTime createdOn) : base(id)
    {
        TenantId = tenantId;
        Title = title;
        Description = description;
        SourceKey = sourceKey;
        Metadata = metadata;
        Thumbnail = thumbnail;
        Specifications = VideoSpecifications.Empty;
        Status = VideoStatus.Processing;
        CreatedOn = createdOn;
    }

    public static ErrorOr<Video> Create(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        string sourceKey,
        VideoMetadata metadata,
        ThumbnailInfo thumbnail,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title))
            return VideoErrors.EmptyTitle;

        return new Video(id, tenantId, title, description, sourceKey, metadata, thumbnail, clock.Now);
    }

    public ErrorOr<Success> MarkAsOptimized(string outputFolder)
    {
        if (Status != VideoStatus.Processing)
            return VideoErrors.InvalidStatus;

        OutputFolder = outputFolder;
        return Result.Success;
    }

    public ErrorOr<Success> MarkAsLive(string finalUrl, IClock clock)
    {
        Status = VideoStatus.Published;
        OutputFolder = finalUrl;
        PublishedOn = clock.Now;

        // Finalize thumbnail URL
        // If finalUrl is "path/to/master.m3u8", get "path/to/"
        string folderPrefix = finalUrl.Contains('/') 
            ? finalUrl[..(finalUrl.LastIndexOf('/') + 1)] 
            : "";

        string thumbFileName = Thumbnail.UseCustom ? "custom.jpg" : "poster.jpg"; 
        string thumbUrl = $"{folderPrefix}thumbnails/{thumbFileName}";

        Thumbnail = new ThumbnailInfo(
            Thumbnail.CustomThumbnailKey, 
            thumbUrl, 
            Thumbnail.UseCustom);

        AddDomainEvent(new VideoPublishedDomainEvent(Id, PublishedOn.Value));

        return Result.Success;
    }

    public ErrorOr<Success> MarkAsPublished(string outputFolder, VideoSpecifications specifications, IClock clock)
    {
        if (Status != VideoStatus.Processing)
            return VideoErrors.InvalidStatus;

        Status = VideoStatus.Published;
        OutputFolder = outputFolder;
        Specifications = specifications;
        PublishedOn = clock.Now;

        AddDomainEvent(new VideoPublishedDomainEvent(Id, PublishedOn.Value));

        return Result.Success;
    }

    public void MarkAsFailed()
    {
        Status = VideoStatus.Failed;
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
