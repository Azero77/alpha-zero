using AlphaZero.Modules.Tenants.Domain;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Tenants.Infrastructure.Consumers;

public class TenantBrandingUpdatedConsumer : 
    IConsumer<TenantBrandingUpdatedDomainEvent>
{
    private readonly HybridCache? _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantBrandingUpdatedConsumer> _logger;

    public TenantBrandingUpdatedConsumer(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TenantBrandingUpdatedConsumer> logger,
        HybridCache? cache = null)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    public async Task Handle(TenantBrandingUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        await ProcessInvalidationAsync(notification.Subdomain, cancellationToken);
    }

    public async Task Consume(ConsumeContext<TenantBrandingUpdatedDomainEvent> context)
    {
        await ProcessInvalidationAsync(context.Message.Subdomain, context.CancellationToken);
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

        // 2. Fire webhook to Next.js API revalidation endpoint
        try
        {
            var baseUrl = _configuration["Frontend:BaseUrl"] ?? throw new ArgumentNullException();
            var secret = _configuration["Frontend:RevalidationSecret"] ?? throw new ArgumentNullException() ;
            var revalidateUrl = $"{baseUrl.TrimEnd('/')}/api/revalidate?tag=tenant-{normalizedSubdomain}&secret={secret}";

            var client = _httpClientFactory.CreateClient("FrontendRevalidation");
            client.Timeout = TimeSpan.FromSeconds(5);

            var request = new HttpRequestMessage(HttpMethod.Post, revalidateUrl);
            request.Headers.Add("x-revalidate-secret", secret);

            var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully triggered Next.js revalidation for tag 'tenant-{Subdomain}'", normalizedSubdomain);
            }
            else
            {
                _logger.LogWarning("Next.js revalidation returned status code {StatusCode} for tenant {Subdomain}", 
                    response.StatusCode, normalizedSubdomain);
            }
        }
        catch (Exception ex)
        {
            // Network failures or offline frontend during dev/tests should not break domain flow
            _logger.LogWarning(ex, "Unable to reach Next.js revalidation endpoint for tenant {Subdomain}", normalizedSubdomain);
        }
    }
}
