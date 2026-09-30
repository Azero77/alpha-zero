using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Linq;

public class AppDbContext : DbContext {}
public class MyConsumer : IConsumer<Ping> { public System.Threading.Tasks.Task Consume(ConsumeContext<Ping> c) => System.Threading.Tasks.Task.CompletedTask; }

public class ModuleConsumerDefinition<TConsumer, TDbContext> : ConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
    where TDbContext : DbContext
{
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<TConsumer> consumerConfigurator, IRegistrationContext context)
    {
        // Actually MT extensions needs the correct using, assuming it's available
        // endpointConfigurator.UseEntityFrameworkOutbox<TDbContext>(context);
    }
}

public static class ModuleExtensions 
{
    public static void AddModuleConsumers<TDbContext>(this IBusRegistrationConfigurator configuration, Assembly assembly)
        where TDbContext : DbContext
    {
        var consumerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IConsumer).IsAssignableFrom(t) && t != typeof(ModuleConsumerDefinition<,>));

        foreach (var consumerType in consumerTypes)
        {
            var definitionType = typeof(ModuleConsumerDefinition<,>).MakeGenericType(consumerType, typeof(TDbContext));
            configuration.AddConsumer(consumerType, definitionType);
        }
    }
}

public class Startup 
{
    public void Config(IServiceCollection services)
    {
        services.AddMassTransit(x => 
        {
            x.AddModuleConsumers<AppDbContext>(typeof(Startup).Assembly);
        });
    }
}
