using AlphaZero.Modules.Courses.Application.Enrollments.Queries.CheckActiveEnrollment;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public sealed class CheckActiveCourseEnrollmentRequestConsumer(ICoursesModule coursesModule)
    : IConsumer<CheckActiveCourseEnrollmentRequest>
{
    public async Task Consume(ConsumeContext<CheckActiveCourseEnrollmentRequest> context)
    {
        var message = context.Message;
        var result = await coursesModule.Send(new CheckActiveEnrollmentQuery(message.TenantId, message.UserId,
            message.TargetResourceArn), context.CancellationToken);

        await context.RespondAsync(result.IsError
            ? new CheckActiveCourseEnrollmentResponse(false, false, result.FirstError.Code)
            : new CheckActiveCourseEnrollmentResponse(true, result.Value));
    }
}
