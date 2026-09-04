namespace AlphaZero.Modules.Assessments.IntegrationEvents;

public record AssessmentGradingCompletedIntegrationEvent(
    Guid SubmissionId,
    Guid AssessmentId,
    Guid StudentId,
    decimal Score,
    bool IsSuccess);
