using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.RemovePrincipalFromUser;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Infrastructure.Consumers.Saga;

public class RemoveStudentRoleFromSagaConsumer(IIdentityModule identityModule, ILogger<RemoveStudentRoleFromSagaConsumer> logger) : IConsumer<RemoveStudentRoleFromSagaCommand>
{
    public async Task Consume(ConsumeContext<RemoveStudentRoleFromSagaCommand> context)
    {
        // Student Principal GUID
        Guid studentPrincipalId = Guid.Parse("00000000-0000-0000-0000-100000000002");
        
        var command = new RemovePrincipalFromUserCommand(
            context.Message.UserId, 
            studentPrincipalId, 
            context.Message.Course.ToString());

        var result = await identityModule.Send(command);

        if (result.IsError)
        {
            logger.LogError("Saga Authorization Removal Failed for User {UserId} in Course {Course}. Error: {Errors}", 
                context.Message.UserId, context.Message.Course.ToString(), string.Join(", ", result.Errors.Select(e => e.Description)));
            
            await context.Publish(new StudentRoleRemovalFailedEvent(context.Message.CorrelationId, "Role removal failed."));
            return;
        }

        await context.Publish(new StudentRoleRemovedFromSagaEvent(context.Message.CorrelationId));
    }
}
