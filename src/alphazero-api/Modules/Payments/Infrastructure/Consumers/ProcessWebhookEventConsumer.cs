using AlphaZero.Modules.Payments.Application;
using AlphaZero.Modules.Payments.Application.Webhooks;
using MassTransit;

namespace AlphaZero.Modules.Payments.Infrastructure.Consumers;

public sealed class ProcessWebhookEventConsumer(IPaymentsModule paymentsModule)
    : IConsumer<ProcessWebhookEventRequested>
{
    public async Task Consume(ConsumeContext<ProcessWebhookEventRequested> context)
    {
        var result = await paymentsModule.Send(new ProcessWebhookEvent(context.Message.EventId),
            context.CancellationToken);
        if (result.IsError)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
