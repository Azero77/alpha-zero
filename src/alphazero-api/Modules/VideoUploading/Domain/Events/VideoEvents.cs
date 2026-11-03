using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.VideoUploading.Domain.Events;

public class VideoPublishedDomainEvent(Guid videoId, Guid tenantId,DateTime publishedOn) : DomainEvent
{
    public Guid VideoId { get; } = videoId;
    public Guid TenantId { get; } = tenantId;
    public DateTime PublishedOn { get; } = publishedOn;
}

public class VideoFailedDomainEvent(Guid videoId, Guid tenantId,string reason) : DomainEvent
{
    public Guid VideoId { get; } = videoId;
    public Guid TenantId { get; } = tenantId;
    public string Reason { get; } = reason;
}

public class VideoMetadataUpdatedDomainEvent : DomainEvent
{
    public Guid VideoId { get; }
    public Guid TenantId { get; }
    public string Title { get; }
    public string? Description { get; }

    public VideoMetadataUpdatedDomainEvent(Guid videoId, Guid tenantId, string title, string? description)
    {
        VideoId = videoId;
        TenantId = tenantId;
        Title = title;
        Description = description;
    }
}
