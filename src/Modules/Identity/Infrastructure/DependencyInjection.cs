using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Modules.Identity.Infrastructure.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.AspNetCore.Authorization.Policy;
using AlphaZero.Shared.Authorization;
using AlphaZero.Modules.Identity.Domain.Services;

namespace AlphaZero.Modules.Identity.Infrastructure;

public static class DependencyInjection
{
    public static void AddIdentityGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__IdentityMigrationHistory");
            });
        });

        services.AddScoped<IPolicyEvaluatorService, PolicyEvaluatorService>();
    }

    public static void AddIdentityPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        moduleServices.AddScoped<IPrincipalRepository, PrincipalRepository>();
        moduleServices.AddScoped<IPolicyRepository, PolicyRepository>();
        moduleServices.AddScoped<IManagedPolicyRepository, ManagedPolicyRepository>();

        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();

        // Register Validators from Application Assembly
        moduleServices.AddValidatorsFromAssembly(typeof(AlphaZero.Modules.Identity.Application.Principals.Commands.CreatePrincipal.CreatePrincipalCommand).Assembly);

        moduleServices.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(typeof(AlphaZero.Modules.Identity.Application.Principals.Commands.CreatePrincipal.CreatePrincipalCommand).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });
    }
}
