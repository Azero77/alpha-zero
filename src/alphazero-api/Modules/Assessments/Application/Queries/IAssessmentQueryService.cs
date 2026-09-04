using AlphaZero.Modules.Assessments.Application.Assessments.Queries.GetAssessment;
using AlphaZero.Modules.Assessments.Application.Assessments.Queries.ListAssessments;
using AlphaZero.Modules.Assessments.Application.Submissions.Queries.GetSubmissions;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Assessments.Application.Queries;

public interface IAssessmentQueryService
{
    Task<AssessmentDetailsDto?> GetAssessmentAsync(Guid id, int? version, CancellationToken cancellationToken = default);
    Task<PagedResult<AssessmentDto>> ListAssessmentsAsync(int page, int perPage, CancellationToken cancellationToken = default);
    Task<PagedResult<SubmissionSummaryDto>> GetSubmissionsAsync(Guid? assessmentId, string? status, int page, int perPage, CancellationToken cancellationToken = default);
}
