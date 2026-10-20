using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Shared.Queries;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(Guid StudentId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<EnrollmentDto>>>;

public class GetStudentEnrollmentsQueryValidator : AbstractValidator<GetStudentEnrollmentsQuery>
{
    public GetStudentEnrollmentsQueryValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PerPage).InclusiveBetween(1, 100);
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
        return await _enrollmentQueryService.ListStudentEnrollmentsAsync(request.StudentId, request.Page, request.PerPage, cancellationToken);
    }
}
