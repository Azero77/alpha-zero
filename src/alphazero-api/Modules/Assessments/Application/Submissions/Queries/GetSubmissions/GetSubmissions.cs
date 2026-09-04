using AlphaZero.Modules.Assessments.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Assessments.Application.Submissions.Queries.GetSubmissions;

public record SubmissionSummaryDto(
    Guid Id,
    Guid AssessmentId,
    Guid StudentId,
    string Status,
    decimal? TotalScore,
    DateTime SubmittedAt);

public record GetSubmissionsQuery(
    Guid? AssessmentId = null,
    string? Status = null,
    int Page = 1,
    int PerPage = 10) : IRequest<ErrorOr<PagedResult<SubmissionSummaryDto>>>;

public sealed class GetSubmissionsQueryHandler(IAssessmentQueryService assessmentQueryService) 
    : IRequestHandler<GetSubmissionsQuery, ErrorOr<PagedResult<SubmissionSummaryDto>>>
{
    public async Task<ErrorOr<PagedResult<SubmissionSummaryDto>>> Handle(GetSubmissionsQuery request, CancellationToken cancellationToken)
    {
        return await assessmentQueryService.GetSubmissionsAsync(
            request.AssessmentId,
            request.Status,
            request.Page,
            request.PerPage,
            cancellationToken);
    }
}
