using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class AppDbContext : DbContext {}
public class MyConsumer : IConsumer<Ping> { public System.Threading.Tasks.Task Consume(ConsumeContext<Ping> c) => System.Threading.Tasks.Task.CompletedTask; }
public class Ping {}

public class Startup 
{
    public void Config(IServiceCollection services)
    {
        services.AddMassTransit(x => 
        {
            x.AddConsumer<MyConsumer>(typeof(ConsumerDefinition<MyConsumer>));
        });
    }
}
