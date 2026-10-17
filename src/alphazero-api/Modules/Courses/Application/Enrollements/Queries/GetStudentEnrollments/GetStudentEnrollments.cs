using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(Guid StudentId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<EnrollmentDto>>>;

public class GetStudentEnrollmentsQueryValidator : AbstractValidator<GetStudentEnrollmentsQuery>
{
    public GetStudentEnrollmentsQueryValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PerPage).ValidPerPage();
    }
}

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

public class GetStudentEnrollmentsForTenantQueryValidator : AbstractValidator<GetStudentEnrollmentsForTenantQuery>
{
    public GetStudentEnrollmentsForTenantQueryValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PerPage).ValidPerPage();
    }
}

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