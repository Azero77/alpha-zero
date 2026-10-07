using AlphaZero.Modules.Documents.Application.Sagas;
using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.IntegrationEvents;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AlphaZero.Shared.Application;

namespace AlphaZero.Modules.Documents.Infrastructure.Consumers;

public class VerifyDocumentDeduplicationConsumer : IConsumer<VerifyDocumentDeduplicationCommand>
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<VerifyDocumentDeduplicationConsumer> _logger;

    public VerifyDocumentDeduplicationConsumer(IDocumentRepository repository, ILogger<VerifyDocumentDeduplicationConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VerifyDocumentDeduplicationCommand> context)
    {
        var cmd = context.Message;
        
        // Find existing document with same hash, tenant, and IsPublic flag
        var existingDoc = await _repository.GetFirst(d => d.TenantId == cmd.TenantId && d.FileHash == cmd.FileHash && d.Id != cmd.DocumentId && d.Status == DocumentStatus.Ready && d.IsPublic == cmd.IsPublic, context.CancellationToken);

        if (existingDoc != null)
        {
            _logger.LogInformation("Found duplicate document {DuplicateId} for {DocumentId}", existingDoc.Id, cmd.DocumentId);
            await context.Publish(new DocumentDeduplicationResultEvent(cmd.DocumentId, true, existingDoc.S3Key, existingDoc.FileHash, existingDoc.Metadata));
        }
        else
        {
            _logger.LogInformation("No duplicate found for {DocumentId}. Triggering Step Function.", cmd.DocumentId);
            await context.Publish(new DocumentDeduplicationResultEvent(cmd.DocumentId, false));
        }
    }
}
public class MarkDocumentFaultedConsumer : IConsumer<MarkDocumentFaultedCommand>
{
    private readonly IDocumentRepository _repository;
    private readonly IUnitOfWork _uow;

    public MarkDocumentFaultedConsumer(IDocumentRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<MarkDocumentFaultedCommand> context)
    {
        var cmd = context.Message;
        var doc = await _repository.GetById(cmd.DocumentId, context.CancellationToken);
        if (doc == null) return;

        doc.MarkAsFaulted();
        _repository.Update(doc);
        await _uow.SaveChangesAsync(context.CancellationToken);
        
        await context.Publish(new DocumentStatusChangedIntegrationEvent(doc.Id, doc.TenantId, doc.Status.ToString()));
    }
}

public class DeleteFaultedDocumentConsumer : IConsumer<DeleteFaultedDocumentCommand>
{
    private readonly IDocumentRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly Application.Services.IDocumentStorageService _storageService;
    private readonly AlphaZero.Shared.Domain.IClock _clock;

    public DeleteFaultedDocumentConsumer(IDocumentRepository repository, IUnitOfWork uow, Application.Services.IDocumentStorageService storageService, AlphaZero.Shared.Domain.IClock clock)
    {
        _repository = repository;
        _uow = uow;
        _storageService = storageService;
        _clock = clock;
    }

    public async Task Consume(ConsumeContext<DeleteFaultedDocumentCommand> context)
    {
        var cmd = context.Message;
        var doc = await _repository.GetById(cmd.DocumentId, context.CancellationToken);
        if (doc == null) return;

        // Delete from S3
        await _storageService.DeleteObjectAsync(doc.S3Key, context.CancellationToken);
        
        doc.MarkAsDeleted(_clock);
        _repository.Update(doc);
        await _uow.SaveChangesAsync(context.CancellationToken);
    }
}
