using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;

public record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    Guid CourseId,
    string Status,
    double CompletionPercentage,
    DateTime EnrolledOn,
    Guid TenantId);

public record GetEnrollementQuery(Guid EnrollmentId) : IRequest<ErrorOr<EnrollmentDto>>;

public sealed class GetEnrollementQueryHandler : IRequestHandler<GetEnrollementQuery, ErrorOr<EnrollmentDto>>
{
    private readonly IEnrollmentQueryService _enrollmentQueryService;

    public GetEnrollementQueryHandler(IEnrollmentQueryService enrollmentQueryService)
    {
        _enrollmentQueryService = enrollmentQueryService;
    }

    public async Task<ErrorOr<EnrollmentDto>> Handle(GetEnrollementQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentQueryService.GetEnrollmentByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        return enrollment;
    }
}
