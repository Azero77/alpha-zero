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
    public async Task<ErrorOr<AssessmentDetailsDto>> Handle(
    GetAssessmentQuery request,
    CancellationToken cancellationToken)
    {
        var result = await _assessmentRepository.Entities
            .Where(a => a.Id == request.Id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                Type = a.Type.ToString(),
                a.PassingScore,
                Status = a.Status.ToString(),

                Version = request.Version.HasValue
                    ? a.Versions
                        .Where(v => v.VersionNumber == request.Version.Value)
                        .Select(v => new
                        {
                            v.VersionNumber,
                            v.Content
                        })
                        .FirstOrDefault()
                    : a.Versions
                        .Where(v => v.Id == a.CurrentVersionId)
                        .Select(v => new
                        {
                            v.VersionNumber,
                            v.Content
                        })
                        .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return Error.NotFound("Assessment.NotFound", "Assessment not found.");
        }

        if (request.Version.HasValue && result.Version is null)
        {
            return Error.NotFound(
                "Assessment.VersionNotFound",
                $"Version {request.Version.Value} not found for this assessment.");
        }

        return new AssessmentDetailsDto(
            result.Id,
            result.Title,
            result.Description,
            result.Type,
            result.PassingScore,
            result.Status,
            result.Version?.VersionNumber ?? 0,
            result.Version?.Content);
    }

}
