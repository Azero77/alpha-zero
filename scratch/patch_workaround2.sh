#!/bin/bash
cat << 'INNER_EOF' > src/alphazero-api/AlphaZero.Shared/Infrastructure/MassTransitMultibusOutboxWorkaround.cs
using System;
using MassTransit;
using MassTransit.DependencyInjection;
using MassTransit.Transports;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Middleware.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlphaZero.Shared.Infrastructure;

public class BusOutboxNotification<TBus> : MassTransit.Middleware.Outbox.BusOutboxNotification, IBusOutboxNotification<TBus> where TBus : class, IBus 
{
    public BusOutboxNotification(IOptions<OutboxDeliveryServiceOptions> options) 
        : base(options ?? Options.Create(new OutboxDeliveryServiceOptions { QueryDelay = TimeSpan.FromSeconds(10) })) 
    {
        if (options == null) Console.WriteLine("OPTIONS IS NULL IN NOTIFICATION");
        else if (options.Value == null) Console.WriteLine("OPTIONS.VALUE IS NULL IN NOTIFICATION");
    }
}

public interface IBusOutboxNotification<TBus> : IBusOutboxNotification where TBus : class, IBus {}

public static class MassTransitMultibusOutboxWorkaround
{
    public static IServiceCollection AddMassTransitMultiBusOutboxWorkaround<TBus, TDbContext>(this IServiceCollection services)
        where TBus : class, IBus
        where TDbContext : DbContext
    {
        services.Configure<OutboxDeliveryServiceOptions>(options => 
        {
            if (options.QueryDelay == TimeSpan.Zero) options.QueryDelay = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<IScopedBusContextProvider<TBus>, MultibusEntityFrameworkScopedBusContextProvider<TBus, TDbContext>>();
        services.AddHostedService<BusOutboxDeliveryService<TBus, TDbContext>>();
        services.AddHostedService<InboxCleanupService<TBus, TDbContext>>();
        services.AddSingleton<IBusOutboxNotification<TBus>, BusOutboxNotification<TBus>>();
        return services;
    }
}

public class MultibusEntityFrameworkScopedBusContextProvider<TBus, TDbContext> : IScopedBusContextProvider<TBus>
    where TBus : class, IBus
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private readonly TBus _bus;
    private readonly IBusOutboxNotification<TBus> _notification;
    private readonly IClientFactory _clientFactory;
    private readonly IServiceProvider _provider;

    public MultibusEntityFrameworkScopedBusContextProvider(TDbContext dbContext, TBus bus, IBusOutboxNotification<TBus> notification, IClientFactory clientFactory, IServiceProvider provider)
    {
        _dbContext = dbContext;
        _bus = bus;
        _notification = notification;
        _clientFactory = clientFactory;
        _provider = provider;
    }

    public void AddContext(ConsumeContext context, IServiceProvider provider)
    {
    }

    public ScopedBusContext Context => new EntityFrameworkScopedBusContext<TBus, TDbContext>(_bus, _dbContext, _notification, _clientFactory, _provider);
}

public class BusOutboxDeliveryService<TBus, TDbContext> : BusOutboxDeliveryService<TDbContext>
    where TBus : class, IBus
    where TDbContext : DbContext
{
    public BusOutboxDeliveryService(
        IBusInstance<TBus> busInstance, 
        IOptions<OutboxDeliveryServiceOptions> options, 
        IOptions<EntityFrameworkOutboxOptions<TDbContext>> efOptions, 
        IBusOutboxNotification<TBus> notification, 
        ILogger<BusOutboxDeliveryService<TDbContext>> logger, 
        IServiceProvider provider)
        : base(busInstance.BusControl, 
               options ?? Options.Create(new OutboxDeliveryServiceOptions { QueryDelay = TimeSpan.FromSeconds(10) }), 
               efOptions, notification, logger, provider)
    {
    }
}

public class InboxCleanupService<TBus, TDbContext> : InboxCleanupService<TDbContext>
    where TBus : class, IBus
    where TDbContext : DbContext
{
    public InboxCleanupService(IOptions<InboxCleanupServiceOptions<TDbContext>> options, ILogger<InboxCleanupService<TDbContext>> logger, IServiceProvider provider)
        : base(options ?? Options.Create(new InboxCleanupServiceOptions<TDbContext>()), logger, provider)
    {
    }
}
INNER_EOF
