using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Commands.Deactivate;

public record DeactivateEnrollmentCommand(Guid UserId, Guid CourseId) : ICommand<Success>;

public sealed class DeactivateEnrollmentCommandHandler(
    IRepository<Enrollement> repository,
    ILogger<DeactivateEnrollmentCommandHandler> logger) : IRequestHandler<DeactivateEnrollmentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeactivateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollments = await repository.Get(e => e.StudentId == request.UserId && e.CourseId == request.CourseId, cancellationToken);
        var enrollment = enrollments.FirstOrDefault();

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
