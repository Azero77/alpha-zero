using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using ErrorOr;
using MediatR;
using System.Collections.Generic;
using System.Linq;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentDashboard;

public record GetStudentDashboardQuery(Guid StudentId) : IRequest<ErrorOr<Dictionary<Guid, List<Enrollement>>>>;

public sealed class GetStudentDashboardQueryHandler : IRequestHandler<GetStudentDashboardQuery, ErrorOr<Dictionary<Guid, List<Enrollement>>>>
{
    private readonly IEnrollementRepository _enrollementRepository;

    public GetStudentDashboardQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<Dictionary<Guid, List<Enrollement>>>> Handle(GetStudentDashboardQuery request, CancellationToken cancellationToken)
    {
        // This query ignores the global tenant filter (via the repository implementation) 
        // to show student enrollments across all academies they are registered in.
        var enrollments = await _enrollementRepository.GetStudentEnrollmentsAcrossTenantsAsync(request.StudentId, cancellationToken);
        
        var grouped = enrollments
            .GroupBy(e => e.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return grouped;
    }
}



