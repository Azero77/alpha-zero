using AlphaZero.Modules.Documents.Domain.Events;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Application.EventHandlers;

public class DocumentDomainEventHandlers :
    INotificationHandler<DocumentMetadataUpdatedDomainEvent>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IModuleBus _publishEndpoint;
    private readonly ILogger<DocumentDomainEventHandlers> _logger;

    public DocumentDomainEventHandlers(
        IDocumentRepository documentRepository,
        IModuleBus publishEndpoint,
        ILogger<DocumentDomainEventHandlers> logger)
    {
        _documentRepository = documentRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(DocumentMetadataUpdatedDomainEvent notification, CancellationToken ct)
    {
        var document = await _documentRepository.GetById(notification.DocumentId, ct);
        if (document == null) return;

        var integrationEvent = new DocumentMetadataChangedIntegrationEvent(
            notification.DocumentId,
            notification.Title,
            notification.Description,
            document.FileType,
            document.FileSizeBytes);

        await _publishEndpoint.Publish(integrationEvent, ct);
        _logger.LogInformation("Published MetadataChanged integration event for Document {Id}", notification.DocumentId);
    }
}
