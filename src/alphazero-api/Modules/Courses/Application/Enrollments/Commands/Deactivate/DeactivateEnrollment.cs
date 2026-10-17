using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;

public record DeactivateEnrollmentCommand(Guid UserId, Guid CourseId) : ICommand<Success>;

public sealed class DeactivateEnrollmentCommandHandler(
    IRepository<Enrollment> repository,
    IAuthorizationContextProvider authorizationContextProvider,
    ILogger<DeactivateEnrollmentCommandHandler> logger) : IRequestHandler<DeactivateEnrollmentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeactivateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await repository.GetFirst(e => e.StudentId == request.UserId && e.CourseId == request.CourseId, cancellationToken);

        if (enrollment is null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found for this user and course.");
        }

        var result = enrollment.Deactivate(EnrollmentTransitionActor.Resolve(authorizationContextProvider));
        if (result.IsError) return result.Errors;

        repository.Update(enrollment);

        logger.LogWarning("Enrollment {EnrollmentId} for User {UserId} in Course {CourseId} has been DEACTIVATED.",
            enrollment.Id, request.UserId, request.CourseId);

        return Result.Success;
    }

}
