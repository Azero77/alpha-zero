using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Assessments.Domain.Events;

public class AssessmentPublishedDomainEvent : DomainEvent
{
    public Guid AssessmentId { get; }
    public AssessmentPublishedDomainEvent(Guid assessmentId) => AssessmentId = assessmentId;
}

public class AssessmentSubmissionSubmittedDomainEvent : DomainEvent
{
    public Guid SubmissionId { get; }
    public Guid AssessmentId { get; }
    public Guid StudentId { get; }

    public AssessmentSubmissionSubmittedDomainEvent(Guid submissionId, Guid assessmentId, Guid studentId)
    {
        SubmissionId = submissionId;
        AssessmentId = assessmentId;
        StudentId = studentId;
    }
}

public class AssessmentGradingCompletedDomainEvent : DomainEvent
{
    public Guid SubmissionId { get; }
    public Guid AssessmentId { get; }
    public Guid StudentId { get; }
    public decimal Score { get; }

    public AssessmentGradingCompletedDomainEvent(Guid submissionId, Guid assessmentId, Guid studentId, decimal score)
    {
        SubmissionId = submissionId;
        AssessmentId = assessmentId;
        StudentId = studentId;
        Score = score;
    }
}
