using MassTransit;

namespace AlphaZero.Modules.Documents.Application.Sagas;

public class DocumentProcessingSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public Guid TenantId { get; set; }
    public string CurrentState { get; set; } = null!;
    public bool IsPublic { get; set; }
    
    public Guid? UploadTimeoutTokenId { get; set; }
    public Guid? FaultedDeletionTokenId { get; set; }
    
    public DateTime CreatedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
}
