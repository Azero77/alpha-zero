using AlphaZero.Modules.Payments.Application;
using AlphaZero.Modules.Payments.Application.Webhooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Payments.Infrastructure.Webhooks;

public sealed class WebhookSweeperWorker(IPaymentsModule paymentsModule, ILogger<WebhookSweeperWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var result = await paymentsModule.Send(
                    new RedriveStaleWebhookEvents(DateTime.UtcNow.AddMinutes(-5)), stoppingToken);
                if (result.IsError)
                    logger.LogError("Webhook sweeper failed: {Errors}",
                        string.Join("; ", result.Errors.Select(error => error.Description)));
                else if (result.Value > 0)
                    logger.LogWarning("Webhook sweeper re-enqueued {Count} stale event(s).", result.Value);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Webhook sweeper pass failed; the next pass will retry.");
            }
        }
    }
}
