using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Assessments.Domain.Events;

public class AssessmentCreatedDomainEvent : DomainEvent
{
    public Guid AssessmentId { get; }
    public string Title { get; }
    public string Type { get; }
    public decimal PassingScore { get; }

    public AssessmentCreatedDomainEvent(Guid assessmentId, string title, string type, decimal passingScore)
    {
        AssessmentId = assessmentId;
        Title = title;
        Type = type;
        PassingScore = passingScore;
    }
}

public class AssessmentMetadataUpdatedDomainEvent : DomainEvent
{
    public Guid AssessmentId { get; }
    public string Title { get; }
    public string Type { get; }
    public decimal PassingScore { get; }

    public AssessmentMetadataUpdatedDomainEvent(Guid assessmentId, string title, string type, decimal passingScore)
    {
        AssessmentId = assessmentId;
        Title = title;
        Type = type;
        PassingScore = passingScore;
    }
}

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
