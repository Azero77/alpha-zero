using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(Guid StudentId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<EnrollmentDto>>>;

public sealed class GetStudentEnrollmentsQueryHandler : IRequestHandler<GetStudentEnrollmentsQuery, ErrorOr<PagedResult<EnrollmentDto>>>
{
    private readonly IEnrollmentQueryService _enrollmentQueryService;

    public GetStudentEnrollmentsQueryHandler(IEnrollmentQueryService enrollmentQueryService)
    {
        _enrollmentQueryService = enrollmentQueryService;
    }

    public async Task<ErrorOr<PagedResult<EnrollmentDto>>> Handle(GetStudentEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        return await _enrollmentQueryService.GetStudentEnrollmentsAsync(request.StudentId, request.Page, request.PerPage, cancellationToken);
    }
}

public record GetStudentEnrollmentsForTenantQuery(Guid StudentId, Guid TenantId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<List<EnrollmentDto>>>;

public sealed class GetStudentEnrollmentsForTenantQueryHandler : IRequestHandler<GetStudentEnrollmentsForTenantQuery, ErrorOr<List<EnrollmentDto>>>
{
    private readonly IEnrollmentQueryService _enrollmentQueryService;

    public GetStudentEnrollmentsForTenantQueryHandler(IEnrollmentQueryService enrollmentQueryService)
    {
        _enrollmentQueryService = enrollmentQueryService;
    }

    public async Task<ErrorOr<List<EnrollmentDto>>> Handle(GetStudentEnrollmentsForTenantQuery request, CancellationToken cancellationToken)
    {
        return await _enrollmentQueryService.GetStudentEnrollmentsForTenantAsync(request.StudentId, request.TenantId, cancellationToken);
    }
}