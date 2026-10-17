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

        var result = enrollment.Deactivate(ActorOf(authorizationContextProvider));
        if (result.IsError) return result.Errors;

        repository.Update(enrollment);

        logger.LogWarning("Enrollment {EnrollmentId} for User {UserId} in Course {CourseId} has been DEACTIVATED.",
            enrollment.Id, request.UserId, request.CourseId);

        return Result.Success;
    }

    /// <summary>
    /// Transitions record who fired them (issue #34 story #10). Admin/support actor comes from the
    /// authorization context; the automation (saga) path carries no principal and stays null.
    /// </summary>
    internal static Guid? ActorOf(IAuthorizationContextProvider provider)
    {
        try
        {
            var contextResult = provider.Get();
            return contextResult.IsError ? null : contextResult.Value?.Id;
        }
        catch
        {
            // contexts outside a request (bus consumers) have no actor
            return null;
        }
    }
}
