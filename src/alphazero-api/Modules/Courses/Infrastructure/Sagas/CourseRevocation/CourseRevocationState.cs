using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRevocation;

public class CourseRevocationState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public Guid AccessCodeId { get; set; }
    public string ResourceArn { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? UnauthorizedAt { get; set; }
    
    public string? FailureReason { get; set; }
}
