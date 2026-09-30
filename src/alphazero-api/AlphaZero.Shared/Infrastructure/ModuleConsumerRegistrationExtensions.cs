using System;
using System.Linq;
using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure;

public class ModuleConsumerDefinition<TConsumer, TDbContext> : ConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
    where TDbContext : DbContext
{
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<TConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseEntityFrameworkOutbox<TDbContext>(context);
    }
}

public static class ModuleConsumerRegistrationExtensions
{
    public static void AddModuleConsumers<TDbContext>(this IBusRegistrationConfigurator configuration, params Assembly[] assemblies)
        where TDbContext : DbContext
    {
        AddModuleConsumers<TDbContext>(configuration, null, assemblies);
    }

    public static void AddModuleConsumers<TDbContext>(this IBusRegistrationConfigurator configuration, Func<Type, bool>? filter, params Assembly[] assemblies)
        where TDbContext : DbContext
    {
        var consumerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IConsumer).IsAssignableFrom(t) && t != typeof(ModuleConsumerDefinition<,>));

        if (filter != null)
        {
            consumerTypes = consumerTypes.Where(filter);
        }

        foreach (var consumerType in consumerTypes)
        {
            var definitionType = typeof(ModuleConsumerDefinition<,>).MakeGenericType(consumerType, typeof(TDbContext));
            configuration.AddConsumer(consumerType, definitionType);
        }
    }
}
