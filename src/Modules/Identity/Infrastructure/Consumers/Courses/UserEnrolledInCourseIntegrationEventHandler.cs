using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;
using AlphaZero.Shared.Application;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Infrastructure.Consumers.Courses;

public class UserEnrolledInCourseIntegrationEventHandler(IMediator mediatr,ILogger<UserEnrolledInCourseIntegrationEventHandler> logger, IModuleBus bus) : IConsumer<UserEnrolledInCourseIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserEnrolledInCourseIntegrationEvent> context)
    {
        // 00000000-0000-0000-0000-100000000002 is the Guid for the Student Principal Template
        Guid principalTemplateForStudent = Guid.Parse("00000000-0000-0000-0000-100000000002");
        
        var command = new AssignPrincipalToUserCommand(
            context.Message.UserId, 
            principalTemplateForStudent, 
            context.Message.Course.ToString());

        var response = await mediatr.Send(command);
        
        if (response.IsError)
        {
            logger.LogError("Error trying to add the Student principal to the User {UserId} for course: {Course}. Error: {Errors}", 
                context.Message.UserId, 
                context.Message.Course.ToString(),
                string.Join(", ", response.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Successfully assigned Student principal to User {UserId} for course {Course}.", 
            context.Message.UserId, context.Message.Course.ToString());
    }
}
