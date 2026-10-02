using AlphaZero.Modules.Assessments.Application.Queries;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Assessments.Application.Assessments.Queries.GetAssessment;

public record AssessmentDetailsDto(
    Guid Id,
    string Title,
    string? Description,
    string Type,
    decimal PassingScore,
    string Status,
    int VersionNumber,
    AssessmentContent? Content);

public record GetAssessmentQuery(Guid Id, int? Version = null) : IRequest<ErrorOr<AssessmentDetailsDto>>;

public sealed class GetAssessmentQueryHandler : IRequestHandler<GetAssessmentQuery, ErrorOr<AssessmentDetailsDto>>
{
    private readonly IAssessmentQueryService _assessmentQueryService;

    public GetAssessmentQueryHandler(IAssessmentQueryService assessmentQueryService)
    {
        _assessmentQueryService = assessmentQueryService;
    }

    public async Task<ErrorOr<AssessmentDetailsDto>> Handle(GetAssessmentQuery request, CancellationToken cancellationToken)
    {
        var result = await _assessmentQueryService.GetAssessmentAsync(request.Id, request.Version, cancellationToken);

        if (result is null)
        {
            return Error.NotFound("Assessment.NotFound", "Assessment not found.");
        }

        if (request.Version.HasValue && result.VersionNumber == -1)
        {
            return Error.NotFound(
                "Assessment.VersionNotFound",
                $"Version {request.Version.Value} not found for this assessment.");
        }

        return result;
    }
}
