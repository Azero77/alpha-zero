using AlphaZero.Modules.Assessments.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Assessments.Application.Assessments.Queries.ListAssessments;

public record AssessmentDto(
    Guid Id,
    string Title,
    string? Description,
    string Type,
    decimal PassingScore,
    string Status);

public record ListAssessmentsQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<AssessmentDto>>>;

public sealed class ListAssessmentsQueryHandler : IRequestHandler<ListAssessmentsQuery, ErrorOr<PagedResult<AssessmentDto>>>
{
    private readonly IAssessmentQueryService _assessmentQueryService;

    public ListAssessmentsQueryHandler(IAssessmentQueryService assessmentQueryService)
    {
        _assessmentQueryService = assessmentQueryService;
    }

    public async Task<ErrorOr<PagedResult<AssessmentDto>>> Handle(ListAssessmentsQuery request, CancellationToken cancellationToken)
    {
        return await _assessmentQueryService.ListAssessmentsAsync(request.Page, request.PerPage, cancellationToken);
    }
}
