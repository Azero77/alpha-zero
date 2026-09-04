using AlphaZero.Modules.Assessments.Application.Assessments.Queries.GetAssessment;
using AlphaZero.Modules.Assessments.Application.Assessments.Queries.ListAssessments;
using AlphaZero.Modules.Assessments.Application.Queries;
using AlphaZero.Modules.Assessments.Application.Submissions.Queries.GetSubmissions;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using AlphaZero.Modules.Assessments.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AlphaZero.Modules.Assessments.Infrastructure.Queries;

public class AssessmentQueryService : IAssessmentQueryService
{
    private readonly AppDbContext _context;

    public AssessmentQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AssessmentDetailsDto?> GetAssessmentAsync(Guid id, int? version, CancellationToken cancellationToken = default)
    {
        var result = await _context.Set<Assessment>()
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                Type = a.Type.ToString(),
                a.PassingScore,
                Status = a.Status.ToString(),

                Version = version.HasValue
                    ? a.Versions
                        .Where(v => v.VersionNumber == version.Value)
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

        if (result is null) return null;

        // If a specific version was requested but not found, we can return a dto with null content,
        // or let the handler check if VersionNumber is 0.
        if (version.HasValue && result.Version is null)
        {
            return new AssessmentDetailsDto(
                result.Id,
                result.Title,
                result.Description,
                result.Type,
                result.PassingScore,
                result.Status,
                -1, // Indicator for version not found
                null);
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

    public async Task<PagedResult<AssessmentDto>> ListAssessmentsAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Assessment>().AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(a => a.Title)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(a => new AssessmentDto(
                a.Id,
                a.Title,
                a.Description,
                a.Type.ToString(),
                a.PassingScore,
                a.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedResult<AssessmentDto>(items, totalCount, page, perPage);
    }

    public async Task<PagedResult<SubmissionSummaryDto>> GetSubmissionsAsync(Guid? assessmentId, string? status, int page, int perPage, CancellationToken cancellationToken = default)
    {
        SubmissionStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubmissionStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var query = _context.Set<AssessmentSubmission>()
            .AsNoTracking()
            .Where(s => (assessmentId == null || s.AssessmentId == assessmentId) &&
                        (statusFilter == null || s.Status == statusFilter) &&
                        s.Status != SubmissionStatus.InProgress);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.SubmittedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(s => new SubmissionSummaryDto(
                s.Id,
                s.AssessmentId,
                s.StudentId,
                s.Status.ToString(),
                s.TotalScore,
                s.SubmittedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<SubmissionSummaryDto>(items, totalCount, page, perPage);
    }
}
