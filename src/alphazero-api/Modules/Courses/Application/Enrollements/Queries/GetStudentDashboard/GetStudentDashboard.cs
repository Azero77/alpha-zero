using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using ErrorOr;
using MediatR;
using System.Collections.Generic;
using System.Linq;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentDashboard;

public record GetStudentDashboardQuery(Guid StudentId) : IRequest<ErrorOr<Dictionary<Guid, List<EnrollmentDto>>>>;

public sealed class GetStudentDashboardQueryHandler : IRequestHandler<GetStudentDashboardQuery, ErrorOr<Dictionary<Guid, List<EnrollmentDto>>>>
{
    private readonly IEnrollmentQueryService _queryService;

    public GetStudentDashboardQueryHandler(IEnrollmentQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<ErrorOr<Dictionary<Guid, List<EnrollmentDto>>>> Handle(GetStudentDashboardQuery request, CancellationToken cancellationToken)
    {
        // This query ignores the global tenant filter to show student enrollments across all academies.
        // It uses the read-only query service instead of the tracked repository.
        var enrollments = await _queryService.GetStudentEnrollmentsAcrossTenantsAsync(request.StudentId, cancellationToken);
        
        var grouped = enrollments
            .GroupBy(e => e.TenantId)
            .ToDictionary(
                g => g.Key, 
                g => g.ToList());

        return grouped;
    }
}



