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
