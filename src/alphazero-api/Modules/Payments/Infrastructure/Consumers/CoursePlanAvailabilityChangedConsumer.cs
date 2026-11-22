using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Payments.Application.Catalog;
using AlphaZero.Modules.Payments.Domain.Catalog;
using MassTransit;
using MediatR;

namespace AlphaZero.Modules.Payments.Infrastructure.Consumers;

/// <summary>Maps the Courses event onto a public Payments Application command.</summary>
public sealed class CoursePlanAvailabilityChangedConsumer(IMediator mediator)
    : IConsumer<CoursePlanAvailabilityChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CoursePlanAvailabilityChangedIntegrationEvent> context)
    {
        var message = context.Message;
        var result = await mediator.Send(new UpsertProductMirror(message.TenantId, "CoursePlan", message.CoursePlanId,
            message.CourseId, message.TargetResourceArn, message.Title,
            message.Availability == CoursePlanAvailability.Available ? SourceAvailability.Available : SourceAvailability.Unavailable,
            message.SourceVersion, message.OccurredAt), context.CancellationToken);
        if (result.IsError)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
