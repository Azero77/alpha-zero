using AlphaZero.Modules.Tenants.Domain;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Tenants.Infrastructure.Consumers;

public class TenantBrandingUpdatedConsumer : 
    IConsumer<TenantBrandingUpdatedDomainEvent>,
    INotificationHandler<TenantBrandingUpdatedDomainEvent>
{
    private readonly HybridCache? _cache;
    private readonly ILogger<TenantBrandingUpdatedConsumer> _logger;

    public TenantBrandingUpdatedConsumer(
        ILogger<TenantBrandingUpdatedConsumer> logger,
        HybridCache? cache = null)
    {
        _logger = logger;
        _cache = cache;
    }
    public async Task Consume(ConsumeContext<TenantBrandingUpdatedDomainEvent> context)
    {
        await ProcessInvalidationAsync(context.Message.Subdomain, context.CancellationToken);
    }

    public async Task Handle(TenantBrandingUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        await ProcessInvalidationAsync(notification.Subdomain, cancellationToken);
    }


    private async Task ProcessInvalidationAsync(string subdomain, CancellationToken cancellationToken)
    {
        var normalizedSubdomain = subdomain.ToLowerInvariant();
        _logger.LogInformation("Processing cache invalidation for tenant branding update: {Subdomain}", normalizedSubdomain);

        // 1. Invalidate backend hybrid cache entry
        if (_cache is not null)
        {
            try
            {
                await _cache.RemoveAsync($"tenant:subdomain:{normalizedSubdomain}", cancellationToken);
                await _cache.RemoveByTagAsync($"tenant-{normalizedSubdomain}", cancellationToken);
                _logger.LogInformation("Removed cache entries for tenant {Subdomain}", normalizedSubdomain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove hybrid cache entry for tenant {Subdomain}", normalizedSubdomain);
            }
        }
    }
}
