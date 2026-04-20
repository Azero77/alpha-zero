using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;

public class EnrollStudentFromSagaConsumer(IMediator mediatr, ILogger<EnrollStudentFromSagaConsumer> logger) : IConsumer<EnrollStudentFromSagaCommand>
{
    public async Task Consume(ConsumeContext<EnrollStudentFromSagaCommand> context)
    {
        var command = new EnrollInCourseCommand(context.Message.UserId, context.Message.CourseId);
        var result = await mediatr.Send(command);

        if (result.IsError)
        {
            logger.LogError("Saga Enrollment Failed for User {UserId} in Course {CourseId}. Error: {Errors}", 
                context.Message.UserId, context.Message.CourseId, string.Join(", ", result.Errors.Select(e => e.Description)));
            
            await context.Publish(new StudentEnrollmentFailedEvent(context.Message.CorrelationId, "Enrollment command failed."));
            return;
        }

        await context.Publish(new StudentEnrolledFromSagaEvent(context.Message.CorrelationId));
    }
}
