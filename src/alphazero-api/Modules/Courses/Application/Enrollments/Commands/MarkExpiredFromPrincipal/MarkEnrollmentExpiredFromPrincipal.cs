using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal;

/// <summary>
/// The lazy expiry signal made concrete (issue #34 rework §3.4 path a, steps 7/8): Identity's
/// <c>PrincipalExpiredIntegrationEvent</c> carries (UserId, TenantId, ResourcePath); the Courses
/// consumer resolves the course id from the ARN path and dispatches this command INSIDE the
/// freshly tenant-scoped module request (the module pipeline is where the transaction, the audit
/// writer and the 1:1 <c>EnrollmentExpired</c> integration event all live per §3.1/§3.4(a)).
/// </summary>
public sealed record MarkEnrollmentExpiredFromPrincipalCommand(Guid UserId, Guid CourseId) : ICommand<Success>;

public sealed class MarkEnrollmentExpiredFromPrincipalCommandHandler(
    IEnrollmentRepository repository,
    ILogger<MarkEnrollmentExpiredFromPrincipalCommandHandler> logger) : IRequestHandler<MarkEnrollmentExpiredFromPrincipalCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(MarkEnrollmentExpiredFromPrincipalCommand request, CancellationToken cancellationToken)
    {
        // Tenant scoping rides the module request's tenant filter (the consumer path is pushed
        // to the event's tenant by TenantConsumeFilter) — a foreign-tenant event resolves to
        // nothing, masked exactly like the HTTP read paths (§3.4 no-leak).
        var enrollment = await repository.GetByStudentAndCourseAsync(request.UserId, request.CourseId, cancellationToken);
        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound",
                $"No enrollment for student {request.UserId} in course {request.CourseId}.");
        }

        var (result, changed) = repository.Transitioned(enrollment,
            () => enrollment.MarkExpired());
        if (result.IsError)
        {
            return result.Errors;
        }

        if (changed)
        {
            logger.LogInformation(
                "Enrollment {EnrollmentId} for Student {StudentId} in Course {CourseId} has been EXPIRED (lazy mark from PrincipalExpired).",
                enrollment.Id, enrollment.StudentId, enrollment.CourseId);
        }

        return Result.Success;
    }
}
