using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;

public record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    Guid CourseId,
    string Status,
    // Transparent derived counts (never persisted per enrollment):
    // completed = completed AND active positions; active = PopCount(course.ActiveItemsBitmask).
    int CompletedItemsCount,
    int ActiveItemsCount,
    double CompletionPercentage,
    DateTime EnrolledOn,
    Guid TenantId);

public record GetEnrollmentQuery(Guid EnrollmentId) : IRequest<ErrorOr<EnrollmentDto>>;

public sealed class GetEnrollmentQueryHandler : IRequestHandler<GetEnrollmentQuery, ErrorOr<EnrollmentDto>>
{
    private readonly IEnrollmentQueryService _enrollmentQueryService;

    public GetEnrollmentQueryHandler(IEnrollmentQueryService enrollmentQueryService)
    {
        _enrollmentQueryService = enrollmentQueryService;
    }

    public async Task<ErrorOr<EnrollmentDto>> Handle(GetEnrollmentQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentQueryService.GetEnrollmentByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        return enrollment;
    }
}
