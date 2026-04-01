using AlphaZero.Modules.VideoUploading.Domain.Events;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public class Video : AggregateRoot, IDomainTenantOwned
{
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public VideoStatus Status { get; private set; }
    public VideoMetadata Metadata { get; private set; } = null!;
    public VideoSpecifications Specifications { get; private set; } = null!;
    public string SourceKey { get; private set; } = null!;
    public string? OutputFolder { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? PublishedOn { get; private set; }
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
        DateTime createdOn) : base(id)
    {
        TenantId = tenantId;
        Title = title;
        Description = description;
        SourceKey = sourceKey;
        Metadata = metadata;
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
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title))
            return VideoErrors.EmptyTitle;

        return new Video(id, tenantId, title, description, sourceKey, metadata, clock.Now);
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
        if (Status != VideoStatus.Processing && Status != VideoStatus.Published) // Published here means "Optimized" in the old logic
            Status = VideoStatus.Published; // We'll keep VideoStatus.Published as the "Live" state

        OutputFolder = finalUrl; // We store the CDN URL here now
        PublishedOn = clock.Now;

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
    }

    public void UpdateMetadata(VideoMetadata metadata)
    {
        Metadata = metadata;
    }

    public void UpdateSpecifications(VideoSpecifications specifications)
    {
        Specifications = specifications;
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
