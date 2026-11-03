using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentValidation;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;

public record DeactivateEnrollmentCommand(Guid UserId, Guid CourseId) : ICommand<Success>;

public class DeactivateEnrollmentCommandValidator : AbstractValidator<DeactivateEnrollmentCommand>
{
    public DeactivateEnrollmentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class DeactivateEnrollmentCommandHandler(
    IRepository<Enrollment> repository,
    IAuthorizationContextProvider authorizationContextProvider,
    EnrollmentStatusTransitionService  enrollmentStatusTransitionService,
    ILogger<DeactivateEnrollmentCommandHandler> logger) : IRequestHandler<DeactivateEnrollmentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeactivateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await repository.GetFirst(e => e.StudentId == request.UserId && e.CourseId == request.CourseId, cancellationToken);

        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found for this user and course.");
        }
        var user = authorizationContextProvider.Get().IsError ? null : authorizationContextProvider.CurrentAuthorizationContext;
        
        var result = await enrollmentStatusTransitionService.Deactivate(enrollment, new EnrollmentStatusTransitionService.EnrollmentChangingStatusMetadata(user?.Id), cancellationToken);
        if (result.IsError) return result.Errors;

        // idempotent no-op (Inactive) marks nothing — the helper resolves the change flag; the
        // saga's revocation log stays unconditional (it cares that the request was processed).


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
    ILogger<DeactivateEnrollmentByIdCommandHandler> logger,
    EnrollmentStatusTransitionService enrollmentStatusTransitionService) : IRequestHandler<DeactivateEnrollmentByIdCommand, ErrorOr<Success>>
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
        var result = await enrollmentStatusTransitionService.Deactivate(enrollment, new EnrollmentStatusTransitionService.EnrollmentChangingStatusMetadata(null), cancellationToken);
        

        logger.LogWarning("Enrollment {EnrollmentId} for User {UserId} in Course {CourseId} has been DEACTIVATED (operator path).",
            enrollment.Id, enrollment.StudentId, enrollment.CourseId);

        return Result.Success;
    }
}
