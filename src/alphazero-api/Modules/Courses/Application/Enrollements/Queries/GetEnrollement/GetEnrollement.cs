using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
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
    private readonly IEnrollementRepository _enrollementRepository;

    public GetEnrollementQueryHandler(IEnrollementRepository enrollementRepository)
    {
        _enrollementRepository = enrollementRepository;
    }

    public async Task<ErrorOr<EnrollmentDto>> Handle(GetEnrollementQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollementRepository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        return new EnrollmentDto(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.Status.ToString(),
            enrollment.Progress.CompletionPercentage,
            enrollment.EnrolledOn,
            enrollment.TenantId);
    }
}
