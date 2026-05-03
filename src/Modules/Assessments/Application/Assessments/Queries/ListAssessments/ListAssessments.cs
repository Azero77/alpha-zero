using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;
using System.Linq;

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
    private readonly IAssessmentRepository _assessmentRepository;

    public ListAssessmentsQueryHandler(IAssessmentRepository assessmentRepository)
    {
        _assessmentRepository = assessmentRepository;
    }

    public async Task<ErrorOr<PagedResult<AssessmentDto>>> Handle(ListAssessmentsQuery request, CancellationToken cancellationToken)
    {
        var result = await _assessmentRepository.Get(
            request.Page,
            request.PerPage,
            orderBy: a => a.Title,
            ascending: true,
            token: cancellationToken);

        return new PagedResult<AssessmentDto>(
            result.Items.Select(a => new AssessmentDto(
                a.Id,
                a.Title,
                a.Description,
                a.Type.ToString(),
                a.PassingScore,
                a.Status.ToString())).ToList(),
            result.TotalCount,
            result.CurrentPage,
            result.PageSize);
    }
}
