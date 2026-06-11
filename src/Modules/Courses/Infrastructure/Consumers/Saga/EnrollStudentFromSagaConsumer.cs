using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers.Saga;

public class EnrollStudentFromSagaConsumer(ICoursesModule coursesModule, ICourseRepository courseRepository, ILogger<EnrollStudentFromSagaConsumer> logger) : IConsumer<EnrollStudentFromSagaCommand>
{
    public async Task Consume(ConsumeContext<EnrollStudentFromSagaCommand> context)
    {
        var course = await courseRepository.GetCourseAsync(context.Message.CourseId);
        if (course == null)
        {
            await context.Publish(new StudentEnrollmentFailedEvent(context.Message.CorrelationId, "Course not found."));
            return;
        }

        var plan = course.Plans.FirstOrDefault(p => string.Equals(p.Name, context.Message.Plan, StringComparison.OrdinalIgnoreCase));
        if (plan == null)
        {
            logger.LogError("Saga Enrollment Failed for User {UserId} in Course {CourseId}. Error: Plan '{Plan}' not found on Course", 
                context.Message.UserId, context.Message.CourseId, context.Message.Plan);
            await context.Publish(new StudentEnrollmentFailedEvent(context.Message.CorrelationId, $"Plan '{context.Message.Plan}' not found on Course"));
            return;
        }

        var command = new EnrollInCourseCommand(context.Message.UserId, context.Message.CourseId);
        var result = await coursesModule.Send(command);

        if (result.IsError)
        {
            logger.LogError("Saga Enrollment Failed for User {UserId} in Course {CourseId}. Error: {Errors}", 
                context.Message.UserId, context.Message.CourseId, string.Join(", ", result.Errors.Select(e => e.Description)));
            
            await context.Publish(new StudentEnrollmentFailedEvent(context.Message.CorrelationId, "Enrollment command failed."));
            return;
        }

        await context.Publish(new StudentEnrolledFromSagaEvent(context.Message.CorrelationId, plan.PrincipalId));
    }
}
