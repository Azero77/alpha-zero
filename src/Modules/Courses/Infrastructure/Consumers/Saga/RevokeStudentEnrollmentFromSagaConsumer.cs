using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Deactivate;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;

public class RevokeStudentEnrollmentFromSagaConsumer(IMediator mediatr, ILogger<RevokeStudentEnrollmentFromSagaConsumer> logger) : IConsumer<RevokeStudentEnrollmentFromSagaCommand>
{
    public async Task Consume(ConsumeContext<RevokeStudentEnrollmentFromSagaCommand> context)
    {
        var command = new DeactivateEnrollmentCommand(context.Message.UserId, context.Message.CourseId);
        var result = await mediatr.Send(command);

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
