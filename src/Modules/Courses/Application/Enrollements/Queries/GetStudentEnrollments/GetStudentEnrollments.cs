using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(Guid StudentId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<Enrollement>>>;

public sealed class GetStudentEnrollmentsQueryHandler : IRequestHandler<GetStudentEnrollmentsQuery, ErrorOr<PagedResult<Enrollement>>>
{
    private readonly IEnrollementRepository _enrollementRepository;

    public GetStudentEnrollmentsQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<PagedResult<Enrollement>>> Handle(GetStudentEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        // This query respects the global tenant filter, returning only enrollments for the current academy.
        return await _enrollementRepository.Get(
            request.Page,
            request.PerPage,
            filter: e => e.StudentId == request.StudentId && e.Status == EnrollementStatus.Active,
            orderBy: e => e.EnrolledOn,
            ascending: false,
            token: cancellationToken);
    }
}



public record GetStudentEnrollmentsForTenantQuery(Guid StudentId, Guid TenantId,int Page = 1, int PerPage = 10) : IRequest<ErrorOr<IReadOnlyCollection<Enrollement>>>;

public sealed class GetStudentEnrollmentsForTenantQueryHandler : IRequestHandler<GetStudentEnrollmentsForTenantQuery, ErrorOr<IReadOnlyCollection<Enrollement>>>
{
    private readonly IEnrollementRepository _enrollementRepository;

    public GetStudentEnrollmentsForTenantQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<IReadOnlyCollection<Enrollement>>> Handle(GetStudentEnrollmentsForTenantQuery request, CancellationToken cancellationToken)
    {
        // This query respects the global tenant filter, returning only enrollments for the current academy.
        return await _enrollementRepository.GetStudentEnrollmentsForTenantAsync(
            request.StudentId,
            request.TenantId,
            cancellationToken);
    }
}