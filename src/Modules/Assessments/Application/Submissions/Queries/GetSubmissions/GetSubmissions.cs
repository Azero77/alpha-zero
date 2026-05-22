using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
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

public sealed class GetSubmissionsQueryHandler(ISubmissionRepository repository) 
    : IRequestHandler<GetSubmissionsQuery, ErrorOr<PagedResult<SubmissionSummaryDto>>>
{
    public async Task<ErrorOr<PagedResult<SubmissionSummaryDto>>> Handle(GetSubmissionsQuery request, CancellationToken cancellationToken)
    {
        SubmissionStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<SubmissionStatus>(request.Status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var result = await repository.Get(
            request.Page,
            request.PerPage,
            s => s.SubmittedAt, // Default sort by SubmittedAt (descending check needed)
            s => (request.AssessmentId == null || s.AssessmentId == request.AssessmentId) &&
                 (statusFilter == null || s.Status == statusFilter) &&
                 s.Status != SubmissionStatus.InProgress, // Teachers only see submitted/under-review/graded
            isDescending: true, // Order by time of submit (newest first)
            token: cancellationToken);

        var dtos = result.Items.Select(s => new SubmissionSummaryDto(
            s.Id,
            s.AssessmentId,
            s.StudentId,
            s.Status.ToString(),
            s.TotalScore,
            s.SubmittedAt)).ToList();

        return new PagedResult<SubmissionSummaryDto>(dtos, result.TotalCount, result.CurrentPage, result.PageSize);
    }
}
