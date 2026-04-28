using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    private readonly IAssessmentRepository _assessmentRepository;

    public GetAssessmentQueryHandler(IAssessmentRepository assessmentRepository)
    {
        _assessmentRepository = assessmentRepository;
    }

    public async Task<ErrorOr<AssessmentDetailsDto>> Handle(GetAssessmentQuery request, CancellationToken cancellationToken)
    {
        // If version is null, get with current version. 
        // Otherwise, we'll need to fetch the specific version.
        
        var query = _assessmentRepository.Entities
            .Where(a => a.Id == request.Id);

        if (request.Version.HasValue)
        {
            // Fetch with specific version number
            query = query.Include(a => a.Versions.Where(v => v.VersionNumber == request.Version.Value));
        }
        else
        {
            // Fetch with current version
            query = query.Include(a => a.Versions.Where(v => v.Id == a.CurrentVersionId));
        }

        var assessment = await query.FirstOrDefaultAsync(cancellationToken);
        
        if (assessment is null)
        {
            return Error.NotFound("Assessment.NotFound", "Assessment not found.");
        }

        var selectedVersion = assessment.Versions.FirstOrDefault();
        
        if (request.Version.HasValue && selectedVersion == null)
        {
            return Error.NotFound("Assessment.VersionNotFound", $"Version {request.Version.Value} not found for this assessment.");
        }

        return new AssessmentDetailsDto(
            assessment.Id,
            assessment.Title,
            assessment.Description,
            assessment.Type.ToString(),
            assessment.PassingScore,
            assessment.Status.ToString(),
            selectedVersion?.VersionNumber ?? 0,
            selectedVersion?.Content);
    }
}
