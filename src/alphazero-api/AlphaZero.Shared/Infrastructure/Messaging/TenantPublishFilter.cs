using AlphaZero.Shared.Infrastructure.Tenats;
using MassTransit;

namespace AlphaZero.Shared.Infrastructure.Messaging;

/// <summary>
/// Stamps the ambient tenant onto the outgoing envelope so consumers whose contract lacks
/// a <c>TenantId</c> property can still restore it.
/// </summary>
public sealed class TenantPublishFilter<T> : IFilter<PublishContext<T>>
    where T : class
{
    private readonly IAmbientTenantContext _ambient;

    public TenantPublishFilter(IAmbientTenantContext ambient) => _ambient = ambient;

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (_ambient.TenantId is { } tenantId)
            context.Headers.Set(TenantConsumeFilter<T>.HeaderName, tenantId.ToString());

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant");
}
