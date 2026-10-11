using AlphaZero.Modules.Documents.Application.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Application.Commands.ProcessSqsMessages;

public record ProcessDocumentUploadedCommand(SQSDocumentUploadedEvent Message) : ICommand<Success>;
public record ProcessDocumentCompletedCommand(SQSDocumentProcessingCompletedEvent Message) : ICommand<Success>;
public record ProcessDocumentFailedCommand(SQSDocumentProcessingFailed Message) : ICommand<Success>;

internal class DocumentProcessedIntegrationEventHanlder : IRequestHandler<ProcessDocumentCompletedCommand,ErrorOr<Success>>
{
    public DocumentProcessedIntegrationEventHanlder(IPublishEndpoint publishEndpoint, IDocumentRepository repo, ILogger<DocumentProcessedIntegrationEventHanlder> logger, IClock clock)
    {
        _publishEndpoint = publishEndpoint;
        _repo = repo;
        _logger = logger;
        _clock = clock;
    }
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IClock _clock;
    private readonly IDocumentRepository _repo;
    private readonly ILogger<DocumentProcessedIntegrationEventHanlder> _logger;
    public async Task<ErrorOr<Success>> Handle(ProcessDocumentCompletedCommand request, CancellationToken cancellationToken)
    {
        
        var msg = request.Message;

        var payloadJson = msg.PayloadJson;
        var document = await _repo.GetById(msg.DocumentId, cancellationToken);
        if (document is null)
        {
            _logger.LogError("Document Id not found in the database for @{documentId}"  , msg.DocumentId);
            return Error.Failure("Document.ProcessFailed.NoId","Document Id not found in the database for @{documentId}", new  Dictionary<string, object>()
            {
                ["documentId"] = msg.DocumentId,
            });
        }
        var process = document.ProcessingCompleted(payloadJson, _clock.Now);
        if (process.IsError)
        {
            _logger.LogError("Processing Failed For Document @{documentId} with Error {error}",  msg.DocumentId, process.FirstError.Description);
            return Error.Failure("Document.ProcessFailed.JsonPayload","Processing Failed For Document @{documentId} with Error @{error}", new  Dictionary<string, object>()
            {
                ["documentId"] = msg.DocumentId,
                ["Error"] = process.FirstError.Description
            });
        }
        _repo.Update(document);
        await _publishEndpoint.Publish(new DocumentProcessingCompletedEvent(
                msg.DocumentId, msg.TenantId, msg.PayloadJson), cancellationToken);
        return Result.Success;
    }
}

public class ProcessDocumentUploadedCommandHandler : IRequestHandler<ProcessDocumentUploadedCommand, ErrorOr<Success>>
{
    private IPublishEndpoint _publishEndpoint;

    public ProcessDocumentUploadedCommandHandler(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }
    public async Task<ErrorOr<Success>> Handle(ProcessDocumentUploadedCommand request, CancellationToken cancellationToken)
    {
        var msg = request.Message;
        await _publishEndpoint.Publish(new DocumentProcessingRequestedEvent(
            msg.DocumentId, msg.TenantId, msg.S3Key, msg.FileHash, msg.Size), cancellationToken);
        return Result.Success;
    }
}

public class ProcessDocumentFailedCommandHandler : IRequestHandler<ProcessDocumentFailedCommand,ErrorOr<Success>>
{
    
    private IPublishEndpoint _publishEndpoint;

    public ProcessDocumentFailedCommandHandler(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }
    public async Task<ErrorOr<Success>> Handle(ProcessDocumentFailedCommand request, CancellationToken cancellationToken)
    {
        
        var msg = request.Message;
        await _publishEndpoint.Publish(new DocumentProcessingFaultedEvent(
            msg.DocumentId, msg.TenantId, msg.ErrorMessage), cancellationToken);
        return Result.Success;
    }
}