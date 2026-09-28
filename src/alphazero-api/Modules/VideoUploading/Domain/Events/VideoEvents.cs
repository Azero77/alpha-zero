using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.VideoUploading.Domain.Events;

public class VideoPublishedDomainEvent(Guid videoId, Guid tenantId,DateTime publishedOn) : DomainEvent
{
    public Guid VideoId { get; } = videoId;
    public Guid TenantId { get; } = tenantId;
    public DateTime PublishedOn { get; } = publishedOn;
}

public class VideoMetadataUpdatedDomainEvent : DomainEvent
{
    public Guid VideoId { get; }
    public string Title { get; }
    public string? Description { get; }

    public VideoMetadataUpdatedDomainEvent(Guid videoId, string title, string? description)
    {
        VideoId = videoId;
        Title = title;
        Description = description;
    }
}
