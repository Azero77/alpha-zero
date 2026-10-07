using AlphaZero.Modules.Documents.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Application.Sagas;

public class DocumentUploadTimeoutEvent
{
    public Guid CorrelationId { get; set; }
}

public class DocumentFaultedDeletionEvent
{
    public Guid CorrelationId { get; set; }
}

public class DocumentProcessingSaga : MassTransitStateMachine<DocumentProcessingSagaState>
{
    private readonly ILogger<DocumentProcessingSaga> _logger;

    public DocumentProcessingSaga(ILogger<DocumentProcessingSaga> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        Event(() => UploadInitiated, x => x.CorrelateById(c => c.Message.DocumentId));
        Event(() => UploadedToStorage, x => x.CorrelateById(c => c.Message.DocumentId));
        Event(() => DeduplicationResult, x => x.CorrelateById(c => c.Message.DocumentId));
        Event(() => ProcessingCompleted, x => x.CorrelateById(c => c.Message.DocumentId));
        Event(() => ProcessingFaulted, x => x.CorrelateById(c => c.Message.DocumentId));

        Schedule(() => UploadTimeout, instance => instance.UploadTimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromHours(24);
            s.Received = r => r.CorrelateById(context => context.Message.CorrelationId);
        });

        Schedule(() => FaultedDeletion, instance => instance.FaultedDeletionTokenId, s =>
        {
            s.Delay = TimeSpan.FromDays(1);
            s.Received = r => r.CorrelateById(context => context.Message.CorrelationId);
        });

        Initially(
            When(UploadInitiated)
                .Then(context =>
                {
                    context.Saga.CorrelationId = context.Message.DocumentId;
                    context.Saga.TenantId = context.Message.TenantId;
                    context.Saga.CreatedOn = DateTime.UtcNow;
                    _logger.LogInformation("Upload initiated for Document {DocumentId}", context.Message.DocumentId);
                })
                .Schedule(UploadTimeout, context => new DocumentUploadTimeoutEvent { CorrelationId = context.Saga.CorrelationId })
                .TransitionTo(Pending)
        );

        During(Pending,
            When(UploadedToStorage)
                .Then(context =>
                {
                    context.Saga.UpdatedOn = DateTime.UtcNow;
                    context.Saga.IsPublic = context.Message.IsPublic;
                    _logger.LogInformation("Document {DocumentId} uploaded to storage. Triggering deduplication check.", context.Message.DocumentId);
                })
                .Unschedule(UploadTimeout)
                /*.Send(context => new VerifyDocumentDeduplicationCommand(
                    context.Saga.CorrelationId,
                    context.Saga.TenantId,
                    context.Message.FileHash,
                    context.Message.S3Key,
                    context.Message.IsPublic))*/
                /*.TransitionTo(VerifyingDuplication),*/
                .TransitionTo(Processing),
                    
            When(UploadTimeout.Received)
                .Then(context =>
                {
                    context.Saga.UpdatedOn = DateTime.UtcNow;
                    _logger.LogWarning("Upload timeout for Document {DocumentId}. Transitioning to Faulted.", context.Saga.CorrelationId);
                })
                .TransitionTo(Faulted)
                .Send(context => new MarkDocumentFaultedCommand(context.Saga.CorrelationId))
                .Schedule(FaultedDeletion, context => new DocumentFaultedDeletionEvent { CorrelationId = context.Saga.CorrelationId })
        );
        /*During(VerifyingDuplication,
            When(DeduplicationResult)
                .IfElse(context => context.Message.IsDuplicate,
                    duplicate => duplicate
                        .Then(context =>
                            _logger.LogInformation("Document {DocumentId} is a duplicate. Linking to existing.",
                                context.Saga.CorrelationId))
                        .Send(context => new CompleteAlreadyFoundBeforeDocumentProcessingCommand(
                            context.Saga.CorrelationId,
                            context.Message.ExistingS3Key,
                            context.Message.ExistingFileHash,
                            context.Message.ExistingMetadata,
                            context.Saga.IsPublic))//we will be working on this feature later, right now the command has no handlers
                        .TransitionTo(Ready),
                    unique => unique
                        .Then(context =>
                            _logger.LogInformation(
                                "Document {DocumentId} is unique. Awaiting Step Function processing.",
                                context.Saga.CorrelationId))
                        .TransitionTo(Processing)));*/
        During(Processing,
            When(ProcessingCompleted)
                .Then(context =>
                {
                    context.Saga.UpdatedOn = DateTime.UtcNow;
                    _logger.LogInformation("Processing completed for Document {DocumentId}.", context.Saga.CorrelationId);
                }).TransitionTo(Ready),

            When(ProcessingFaulted)
                .Then(context =>
                {
                    context.Saga.UpdatedOn = DateTime.UtcNow;
                    _logger.LogError("Processing failed for Document {DocumentId}. Reason: {Reason}", context.Saga.CorrelationId, context.Message.ErrorMessage);
                })
                .TransitionTo(Faulted)
                .Send(context => new MarkDocumentFaultedCommand(context.Saga.CorrelationId))
                .Schedule(FaultedDeletion, context => new DocumentFaultedDeletionEvent { CorrelationId = context.Saga.CorrelationId })
        );

        During(Faulted,
            When(FaultedDeletion.Received)
                .Then(context => _logger.LogInformation("Executing scheduled deletion for faulted Document {DocumentId}.", context.Saga.CorrelationId))
                .Send(context => new DeleteFaultedDocumentCommand(context.Saga.CorrelationId))
                .Finalize()
        );
        
        SetCompletedWhenFinalized();
    }

    public State Pending { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Ready { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;
    public State VerifyingDuplication { get; private set; } = null!;

    public Event<DocumentUploadInitiatedEvent> UploadInitiated { get; private set; } = null!;
    public Event<DocumentUploadedToStorageEvent> UploadedToStorage { get; private set; } = null!;
    public Event<DocumentDeduplicationResultEvent> DeduplicationResult { get; private set; } = null!;
    public Event<DocumentProcessingCompletedEvent> ProcessingCompleted { get; private set; } = null!;
    public Event<DocumentProcessingFaultedEvent> ProcessingFaulted { get; private set; } = null!;
    
    public Schedule<DocumentProcessingSagaState, DocumentUploadTimeoutEvent> UploadTimeout { get; private set; } = null!;
    public Schedule<DocumentProcessingSagaState, DocumentFaultedDeletionEvent> FaultedDeletion { get; private set; } = null!;
}

public record VerifyDocumentDeduplicationCommand(Guid DocumentId, Guid TenantId, string FileHash, string CurrentS3Key, bool IsPublic);
public record DocumentDeduplicationResultEvent(Guid DocumentId, bool IsDuplicate, string? ExistingS3Key = null, string? ExistingFileHash = null, System.Collections.Generic.Dictionary<string, object>? ExistingMetadata = null);
public record CompleteAlreadyFoundBeforeDocumentProcessingCommand(Guid DocumentId, string? S3Key, string? FileHash, System.Collections.Generic.Dictionary<string, object>? Metadata, bool IsPublic);
public record MarkDocumentFaultedCommand(Guid DocumentId);
public record DeleteFaultedDocumentCommand(Guid DocumentId);
