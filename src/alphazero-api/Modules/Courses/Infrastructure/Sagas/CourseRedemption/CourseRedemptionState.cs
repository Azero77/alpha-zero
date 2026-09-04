using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRedemption;

public class CourseRedemptionState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public Guid AccessCodeId { get; set; }
    public string CourseArn { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    
    public string? FailureReason { get; set; }
}
