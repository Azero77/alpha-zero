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

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.Suspend;

public record SuspendEnrollmentCommand(Guid EnrollmentId, string? Reason) : ICommand<Success>;

public class SuspendEnrollmentCommandValidator : AbstractValidator<SuspendEnrollmentCommand>
{
    public SuspendEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}

/// <summary>
/// Support/billing suspension (issue #34 story #3): distinct from deactivation — reversible pause
/// of access while e.g. a billing dispute resolves. Idempotent (already-suspended succeeds).
/// </summary>
public sealed class SuspendEnrollmentCommandHandler(
    IEnrollmentRepository repository,
    IAuthorizationContextProvider authorizationContextProvider,
    ILogger<SuspendEnrollmentCommandHandler> logger) : IRequestHandler<SuspendEnrollmentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(SuspendEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await repository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");
        }

        var previousStatus = enrollment.Status;
        var result = enrollment.Suspend(EnrollmentTransitionActor.Resolve(authorizationContextProvider), request.Reason);
        if (result.IsError) return result.Errors;

        if (previousStatus != enrollment.Status)
        {
            repository.Update(enrollment);
            logger.LogWarning("Enrollment {EnrollmentId} for Student {StudentId} in Course {CourseId} has been SUSPENDED (reason: {Reason}).",
                enrollment.Id, enrollment.StudentId, enrollment.CourseId, request.Reason ?? "unspecified");
        }

        return Result.Success;
    }
}
