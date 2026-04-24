using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Assessments.Domain.Events;

public record AssessmentPublishedDomainEvent(Guid AssessmentId) : DomainEvent;
public record AssessmentSubmissionSubmittedDomainEvent(Guid SubmissionId, Guid AssessmentId, Guid StudentId) : DomainEvent;
public record AssessmentGradingCompletedDomainEvent(Guid SubmissionId, Guid AssessmentId, Guid StudentId, decimal Score) : DomainEvent;
