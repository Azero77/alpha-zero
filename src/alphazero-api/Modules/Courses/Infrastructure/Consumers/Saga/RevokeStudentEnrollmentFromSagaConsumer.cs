using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;

public class RevokeStudentEnrollmentFromSagaConsumer(ICoursesModule coursesModule, ILogger<RevokeStudentEnrollmentFromSagaConsumer> logger) : IConsumer<RevokeStudentEnrollmentFromSagaCommand>
{
    public async Task Consume(ConsumeContext<RevokeStudentEnrollmentFromSagaCommand> context)
    {
        var command = new DeactivateEnrollmentCommand(context.Message.UserId, context.Message.CourseId);

        // Story #10 (§3.5): the saga is one of two allowed actor kinds for enrollment
        // deactivation, and it must CLAIM its transport origin — the consumer's module dispatch
        // has no HTTP actor context, which otherwise looks identical to a rogue in-process send
        // (the story-#10 guard refuses those before the aggregate is loaded).
        using var _ = EnrollmentTransitionAutomation.ClaimSagaTransport();
        var result = await coursesModule.Send(command);

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
