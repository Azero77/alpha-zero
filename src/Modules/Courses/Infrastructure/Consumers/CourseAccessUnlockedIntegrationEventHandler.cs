using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using Amazon.Runtime.Internal.Util;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class CourseAccessUnlockedIntegrationEventHandler(ILogger<CourseAccessUnlockedIntegrationEventHandler> logger, IMediator mediatr, IModuleBus bus) : IConsumer<CourseAccessUnlockedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CourseAccessUnlockedIntegrationEvent> context)
    {
        Guid courseId = Guid.Empty;
        try
        {
             courseId = context.Message.Resource.GetCourseId();//the resource path should be courses/course/math101
        }
        catch (Exception)
        {
            logger.LogError("Error Extracting key for access code {AccessCode} for {Resource}", context.Message.AccessCodeId,context.Message.Resource);
            return;
        }
        var enrollCommand = new EnrollInCourseCommand(context.Message.UserId,courseId);
        var response = await mediatr.Send(enrollCommand);
        if(response.IsError)
        {
            logger.LogError("Error Enrollement for access code {AccessCode} for {Resource}", context.Message.AccessCodeId, context.Message.Resource);
            return;
        }
        await bus.Publish(new UserEnrolledInCourseIntegrationEvent(context.Message.UserId, context.Message.Resource));
    }

}