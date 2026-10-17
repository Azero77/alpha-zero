using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.Reactivate;

public record ReactivateEnrollmentCommand(Guid EnrollmentId) : ICommand<Success>;

public class ReactivateEnrollmentCommandValidator : AbstractValidator<ReactivateEnrollmentCommand>
{
    public ReactivateEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
    }
}

/// <summary>
/// Support/admin reactivation (issue #34 stories #1/#2/#4): moves an Inactive/Expired/Suspended
/// (idempotently: already-Active) enrollment back to Active, preserving earned progress. Mirrors
/// the revocation flow in reverse — a resumed payment/authorization can restore the student.
/// </summary>
public sealed class ReactivateEnrollmentCommandHandler(
    IEnrollmentRepository repository,
    IAuthorizationContextProvider authorizationContextProvider,
    ILogger<ReactivateEnrollmentCommandHandler> logger) : IRequestHandler<ReactivateEnrollmentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReactivateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await repository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");
        }

        var previousStatus = enrollment.Status;
        var result = enrollment.Reactivate(EnrollmentTransitionActor.Resolve(authorizationContextProvider));
        if (result.IsError) return result.Errors;

        if (previousStatus != enrollment.Status)
        {
            repository.Update(enrollment);
            logger.LogInformation("Enrollment {EnrollmentId} for Student {StudentId} in Course {CourseId} has been REACTIVATED.",
                enrollment.Id, enrollment.StudentId, enrollment.CourseId);
        }

        return Result.Success;
    }
}
