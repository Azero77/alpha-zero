using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public interface IModuleBus : IBus {}
public class AppDbContext : DbContext {}

public class Test {
    public void Configure(IBusRegistrationConfigurator cfg) {
        cfg.AddEntityFrameworkOutbox<IModuleBus, AppDbContext>(o => o.UsePostgres());
    }
}
