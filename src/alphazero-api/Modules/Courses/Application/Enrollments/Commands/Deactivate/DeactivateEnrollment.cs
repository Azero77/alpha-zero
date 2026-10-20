using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentValidation;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;

public record DeactivateEnrollmentCommand(Guid UserId, Guid CourseId) : ICommand<Success>;

public sealed class DeactivateEnrollmentCommandHandler(
    IRepository<Enrollment> repository,
    ILogger<DeactivateEnrollmentCommandHandler> logger) : IRequestHandler<DeactivateEnrollmentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeactivateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await repository.GetFirst(e => e.StudentId == request.UserId && e.CourseId == request.CourseId, cancellationToken);

        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found for this user and course.");
        }

        enrollment.Deactivate();
        repository.Update(enrollment);

        logger.LogWarning("Enrollment {EnrollmentId} for User {UserId} in Course {CourseId} has been DEACTIVATED.", 
            enrollment.Id, request.UserId, request.CourseId);

        return Result.Success;
    }
}

// Id-keyed operator command for the admin/support GraphQL/HTTP surface (#29). The saga-keyed
// DeactivateEnrollmentCommand (studentId/courseId) above keeps serving the revocation saga
// consumer unchanged.
public record DeactivateEnrollmentByIdCommand(Guid EnrollmentId) : ICommand<Success>;

public class DeactivateEnrollmentByIdCommandValidator : AbstractValidator<DeactivateEnrollmentByIdCommand>
{
    public DeactivateEnrollmentByIdCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
    }
}

public sealed class DeactivateEnrollmentByIdCommandHandler(
    IEnrollmentRepository repository,
    ILogger<DeactivateEnrollmentByIdCommandHandler> logger) : IRequestHandler<DeactivateEnrollmentByIdCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeactivateEnrollmentByIdCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await repository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");
        }

        // Idempotent by contract: retrying the operation on an already-inactive enrollment succeeds.
        if (enrollment.Status == EnrollmentStatus.Inactive)
        {
            logger.LogInformation("Enrollment {EnrollmentId} is already inactive; deactivation is idempotent.", request.EnrollmentId);
            return Result.Success;
        }

        enrollment.Deactivate();
        repository.Update(enrollment);

        logger.LogWarning("Enrollment {EnrollmentId} for User {UserId} in Course {CourseId} has been DEACTIVATED (operator path).",
            enrollment.Id, enrollment.StudentId, enrollment.CourseId);

        return Result.Success;
    }
}
