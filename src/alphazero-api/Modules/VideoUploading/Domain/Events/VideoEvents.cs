using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.VideoUploading.Domain.Events;

public class VideoPublishedDomainEvent : DomainEvent
{
    public Guid VideoId { get; }
    public DateTime PublishedOn { get; }

    public VideoPublishedDomainEvent(Guid videoId, DateTime publishedOn)
    {
        VideoId = videoId;
        PublishedOn = publishedOn;
    }
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
