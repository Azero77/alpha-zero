using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Authorization;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;

public class RevokeStudentEnrollmentFromSagaConsumer(ICoursesModule coursesModule, ILogger<RevokeStudentEnrollmentFromSagaConsumer> logger) : IConsumer<RevokeStudentEnrollmentFromSagaCommand>
{
    public async Task Consume(ConsumeContext<RevokeStudentEnrollmentFromSagaCommand> context)
    {
        var command = new DeactivateEnrollmentCommand(context.Message.UserId, context.Message.CourseId);

        // Story #10 (§3.5): the saga is an authorized system machine identity in IAM.
        // We pass the SystemServiceAuthorizationContext explicitly to the module dispatch,
        // seeding the child lifetime scope with honest actor credentials (no AsyncLocal).
        var systemContext = SystemServiceAuthorizationContext.Create(
            EnrollmentTransitionActor.RevocationSagaServiceName,
            EnrollmentPermissions.Deactivate);
        var result = await coursesModule.Send(command, systemContext, context.CancellationToken);

        if (result.IsError)
        {
            logger.LogError("Saga Enrollment Revocation Failed for User {UserId} in Course {CourseId}. Error: {Errors}", 
                context.Message.UserId, context.Message.CourseId, string.Join(", ", result.Errors.Select(e => e.Description)));
            
            await context.Publish(new StudentEnrollmentRevocationFailedEvent(context.Message.CorrelationId, "Enrollment revocation command failed."));
            return;
        }

        await context.Publish(new StudentEnrollmentRevokedFromSagaEvent(context.Message.CorrelationId));
    }
}
