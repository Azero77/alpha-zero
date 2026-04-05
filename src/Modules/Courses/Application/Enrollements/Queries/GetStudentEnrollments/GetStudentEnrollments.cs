using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Shared.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using ErrorOr;
using MediatR;
using System.Linq;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(Guid StudentId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<EnrollmentDto>>>;

public sealed class GetStudentEnrollmentsQueryHandler : IRequestHandler<GetStudentEnrollmentsQuery, ErrorOr<PagedResult<EnrollmentDto>>>
{
    private readonly IEnrollementRepository _enrollementRepository;

    public GetStudentEnrollmentsQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<PagedResult<EnrollmentDto>>> Handle(GetStudentEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        // This query respects the global tenant filter, returning only enrollments for the current academy.
        var result = await _enrollementRepository.Get(
            request.Page,
            request.PerPage,
            filter: e => e.StudentId == request.StudentId && e.Status == EnrollementStatus.Active,
            orderBy: e => e.EnrolledOn,
            ascending: false,
            token: cancellationToken);

        return new PagedResult<EnrollmentDto>(
            result.Items.Select(enrollment => new EnrollmentDto(
                enrollment.Id,
                enrollment.StudentId,
                enrollment.CourseId,
                enrollment.Status.ToString(),
                enrollment.Progress.CompletionPercentage,
                enrollment.EnrolledOn,
                enrollment.TenantId)).ToList(),
            result.TotalCount,
            result.CurrentPage,
            result.PageSize);
    }
}



public record GetStudentEnrollmentsForTenantQuery(Guid StudentId, Guid TenantId,int Page = 1, int PerPage = 10) : IRequest<ErrorOr<IReadOnlyCollection<EnrollmentDto>>>;

public sealed class GetStudentEnrollmentsForTenantQueryHandler : IRequestHandler<GetStudentEnrollmentsForTenantQuery, ErrorOr<IReadOnlyCollection<EnrollmentDto>>>
{
    private readonly IEnrollementRepository _enrollementRepository;

    public GetStudentEnrollmentsForTenantQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<IReadOnlyCollection<EnrollmentDto>>> Handle(GetStudentEnrollmentsForTenantQuery request, CancellationToken cancellationToken)
    {
        // This query respects the global tenant filter, returning only enrollments for the current academy.
        var enrollments = await _enrollementRepository.GetStudentEnrollmentsForTenantAsync(
            request.StudentId,
            request.TenantId,
            cancellationToken);

        return enrollments.Select(enrollment => new EnrollmentDto(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.Status.ToString(),
            enrollment.Progress.CompletionPercentage,
            enrollment.EnrolledOn,
            enrollment.TenantId)).ToList().AsReadOnly();
    }
}