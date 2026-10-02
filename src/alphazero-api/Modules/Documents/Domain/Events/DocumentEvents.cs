using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Documents.Domain.Events;

public class DocumentMetadataUpdatedDomainEvent : DomainEvent
{
    public Guid DocumentId { get; }
    public string Title { get; }
    public string? Description { get; }

    public DocumentMetadataUpdatedDomainEvent(Guid documentId, string title, string? description)
    {
        DocumentId = documentId;
        Title = title;
        Description = description;
    }
}
