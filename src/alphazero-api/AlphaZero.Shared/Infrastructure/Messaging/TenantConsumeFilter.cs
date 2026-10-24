using System.Reflection;
using AlphaZero.Shared.Infrastructure.Tenats;
using MassTransit;

namespace AlphaZero.Shared.Infrastructure.Messaging;

/// <summary>
/// Restores the tenant for a consumed message from its <c>TenantId</c> property or the
/// envelope header stamped by <see cref="TenantPublishFilter{T}"/>. Without this, consumers
/// run outside an HTTP request and every tenant-filtered query silently returns nothing.
/// </summary>
public sealed class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    public const string HeaderName = "TenantId";

    private static readonly PropertyInfo? TenantProperty =
        typeof(T).GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance);

    private readonly IAmbientTenantContext _ambient;

    public TenantConsumeFilter(IAmbientTenantContext ambient) => _ambient = ambient;

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        using var _ = _ambient.Push(ResolveTenant(context));
        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant");

    private Guid? ResolveTenant(ConsumeContext<T> context)
    {
        if (TenantProperty?.GetValue(context.Message) is Guid messageTenant && messageTenant != Guid.Empty)
            return messageTenant;

        if (context.Headers.TryGetHeader(HeaderName, out var header)
            && header is string headerValue
            && Guid.TryParse(headerValue, out var headerTenant)
            && headerTenant != Guid.Empty)
        {
            return headerTenant;
        }

        return null;
    }
}
