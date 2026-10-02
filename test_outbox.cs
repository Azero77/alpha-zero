using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class DbContext1 : DbContext {}
public class DbContext2 : DbContext {}

public class Test {
    public void Configure(IServiceCollection services) {
        services.AddMassTransit(x => {
            x.AddEntityFrameworkOutbox<DbContext1>(o => o.UsePostgres());
            x.AddEntityFrameworkOutbox<DbContext2>(o => o.UsePostgres());
        });
    }
}
