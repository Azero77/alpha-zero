using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Infrastructure.Persistance;
using AlphaZero.Modules.Tenants.Infrastructure.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using AlphaZero.Shared.Presentation.Extensions;

namespace AlphaZero.Modules.Tenants.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTenantsGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__TenantsMigrationHistory", AppDbContext.Schema);
            });
            opts.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });

        return services;
    }

    public static IServiceCollection AddTenantsPrivateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
        services.AddScoped<AlphaZero.Modules.Tenants.Application.Queries.ITenantQueryService, AlphaZero.Modules.Tenants.Infrastructure.Queries.TenantQueryService>();

        var applicationAssembly = typeof(AlphaZero.Modules.Tenants.Application.Tenants.Commands.CreateTenant.CreateTenantCommand).Assembly;
        
        services.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(applicationAssembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        return services;
    }
}
