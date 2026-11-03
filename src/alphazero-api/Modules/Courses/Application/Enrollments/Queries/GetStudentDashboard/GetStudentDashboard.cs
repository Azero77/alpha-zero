using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using MediatR;
using System.Collections.Generic;
using System.Linq;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentDashboard;

/// <summary>
/// Exactly what the dashboard needs per row: the seven fields it serializes plus the TenantId
/// the handler groups them by. Nothing else — no StudentId, no re-mapping from the wider
/// support <c>EnrollmentDto</c>.
/// </summary>
public record DashboardEnrollmentRow(
    Guid EnrollmentId,
    Guid CourseId,
    string Status,
    int CompletedItemsCount,
    int ActiveItemsCount,
    double CompletionPercentage,
    DateTime EnrolledOn,
    Guid TenantId);

public record GetStudentDashboardQuery(Guid StudentId) : IRequest<ErrorOr<Dictionary<Guid, List<DashboardEnrollmentRow>>>>;

public sealed class GetStudentDashboardQueryHandler : IRequestHandler<GetStudentDashboardQuery, ErrorOr<Dictionary<Guid, List<DashboardEnrollmentRow>>>>
{
    private readonly IEnrollmentQueryService _queryService;

    public GetStudentDashboardQueryHandler(IEnrollmentQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<ErrorOr<Dictionary<Guid, List<DashboardEnrollmentRow>>>> Handle(GetStudentDashboardQuery request, CancellationToken cancellationToken)
    {
        // This query ignores the global tenant filter to show student enrollments across all academies.
        // It uses the read-only query service instead of the tracked repository.
        var enrollments = await _queryService.ListStudentDashboardEnrollmentsAsync(request.StudentId, cancellationToken);

        var grouped = enrollments
            .GroupBy(e => e.TenantId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        return grouped;
    }
}
