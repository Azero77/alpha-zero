using AlphaZero.Modules.Documents.Application.Models;
using AlphaZero.Modules.Documents.IntegrationEvents;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace AlphaZero.Modules.Documents.Application.Commands.ProcessSqsMessages;

public record ProcessDocumentUploadedCommand(DocumentUploadedQueueMessage Message) : IRequest;
public record ProcessDocumentCompletedCommand(DocumentProcessingCompletedQueueMessage Message) : IRequest;
public record ProcessDocumentFailedCommand(DocumentProcessingFailedQueueMessage Message) : IRequest;

public class ProcessSqsCommandsHandler : 
    IRequestHandler<ProcessDocumentUploadedCommand>,
    IRequestHandler<ProcessDocumentCompletedCommand>,
    IRequestHandler<ProcessDocumentFailedCommand>
{
    private readonly MassTransit.IPublishEndpoint _publishEndpoint;

    public ProcessSqsCommandsHandler(MassTransit.IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(ProcessDocumentUploadedCommand request, CancellationToken cancellationToken)
    {
        var msg = request.Message;
        await _publishEndpoint.Publish(new DocumentUploadedToStorageEvent(
            msg.DocumentId, msg.TenantId, msg.S3Key, msg.FileHash, msg.Size), cancellationToken);
    }

    public async Task Handle(ProcessDocumentCompletedCommand request, CancellationToken cancellationToken)
    {
        var msg = request.Message;
        await _publishEndpoint.Publish(new DocumentProcessingCompletedEvent(
            msg.DocumentId, msg.TenantId, msg.PayloadJson), cancellationToken);
    }

    public async Task Handle(ProcessDocumentFailedCommand request, CancellationToken cancellationToken)
    {
        var msg = request.Message;
        await _publishEndpoint.Publish(new DocumentProcessingFaultedEvent(
            msg.DocumentId, msg.TenantId, msg.ErrorMessage), cancellationToken);
    }
}
