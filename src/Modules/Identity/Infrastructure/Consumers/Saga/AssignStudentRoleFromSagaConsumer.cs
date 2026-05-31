using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Infrastructure.Consumers.Saga;

public class AssignStudentRoleFromSagaConsumer(IIdentityModule identityModule, ILogger<AssignStudentRoleFromSagaConsumer> logger) : IConsumer<AssignStudentRoleFromSagaCommand>
{
    public async Task Consume(ConsumeContext<AssignStudentRoleFromSagaCommand> context)
    {
        // Student Principal GUID
        Guid studentPrincipalId = Guid.Parse("00000000-0000-0000-0000-100000000002");
        
        var command = new AssignPrincipalToUserCommand(
            context.Message.UserId, 
            studentPrincipalId, 
            context.Message.Course.ToString());

        var result = await identityModule.Send(command);

        if (result.IsError)
        {
            logger.LogError("Saga Authorization Failed for User {UserId} in Course {Course}. Error: {Errors}", 
                context.Message.UserId, context.Message.Course.ToString(), string.Join(", ", result.Errors.Select(e => e.Description)));
            
            await context.Publish(new StudentRoleAssignmentFailedEvent(context.Message.CorrelationId, "Role assignment failed."));
            return;
        }

        await context.Publish(new StudentRoleAssignedFromSagaEvent(context.Message.CorrelationId));
    }
}
