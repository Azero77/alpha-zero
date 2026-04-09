using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Modules.Identity.Infrastructure.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure;
using Autofac.Core;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        //they are public because it is used by fast endpoint middleware in the api scope
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IManagedPolicyRepository, ManagedPolicyRepository>();
        services.AddScoped<IPrincipalRepository, PrincipalRepository>();
        services.AddScoped<IPolicyEvaluatorService, PolicyEvaluatorService>();
        services.AddScoped<PolicyEvaluatorService>();

    }

    public static void AddIdentityPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
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
