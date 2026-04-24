using AlphaZero.Modules.Assessments.Domain.Events;
using AlphaZero.Modules.Assessments.Domain.Models.Submissions;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;

public class AssessmentSubmission : TenantOwnedAggregate
{
    public Guid AssessmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public decimal? TotalScore { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime? GradedAt { get; private set; }
    
    // JSONB Responses
    public AssessmentSubmissionResponses Responses { get; private set; } = new();

    private AssessmentSubmission() { }

    private AssessmentSubmission(Guid id, Guid tenantId, Guid assessmentId, Guid studentId) 
        : base(id, tenantId)
    {
        AssessmentId = assessmentId;
        StudentId = studentId;
        Status = SubmissionStatus.InProgress;
    }

    public static AssessmentSubmission Create(Guid id, Guid tenantId, Guid assessmentId, Guid studentId)
    {
        return new AssessmentSubmission(id, tenantId, assessmentId, studentId);
    }

    public ErrorOr<Success> Submit(AssessmentSubmissionResponses responses)
    {
        if (Status != SubmissionStatus.InProgress)
            return Error.Conflict("Submission.Status", "Only in-progress submissions can be submitted.");

        if (responses.Answers.Count == 0)
            return Error.Validation("Submission.Empty", "Cannot submit an empty response.");

        Responses = responses;
        SubmittedAt = DateTime.UtcNow;
        Status = SubmissionStatus.Submitted;
        
        AddDomainEvent(new AssessmentSubmissionSubmittedDomainEvent(Id, AssessmentId, StudentId));
        return Result.Success;
    }

    public ErrorOr<Success> MarkAsUnderReview()
    {
        if (Status != SubmissionStatus.Submitted)
            return Error.Conflict("Submission.Status", "Only submitted assessments can be marked for review.");

        Status = SubmissionStatus.UnderReview;
        return Result.Success;
    }

    public ErrorOr<Success> FinalizeGrading(decimal totalScore)
    {
        if (Status == SubmissionStatus.Graded)
            return Error.Conflict("Submission.Status", "Submission is already graded.");

        TotalScore = totalScore;
        GradedAt = DateTime.UtcNow;
        Status = SubmissionStatus.Graded;

        AddDomainEvent(new AssessmentGradingCompletedDomainEvent(Id, AssessmentId, StudentId, totalScore));
        return Result.Success;
    }
}

public enum SubmissionStatus
{
    InProgress,
    Submitted,
    UnderReview,
    Graded
}
