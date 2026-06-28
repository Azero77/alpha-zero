using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Modules.Identity.Infrastructure.Auth;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Modules.Identity.Infrastructure.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Repositores;
using Autofac.Core;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Hybrid;
using AlphaZero.Modules.Identity.Domain.Models.Principals;

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
            opts.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        //they are public because it is used by fast endpoint middleware in the api scope
        services.AddScoped<IManagedPolicyRepository, ManagedPolicyRepository>();
        services.AddScoped<IPrincipalRepository, PrincipalRepository>();
        services.AddScoped<IRepository<TenantUser>,TenantUserRepository>(); 
        services.AddScoped<ITenantUserPrincipalAssignmentRepository, TenantUserPrincipalAssignmentRepository>();
        services.Decorate<ITenantUserPrincipalAssignmentRepository, CachingTenantUserPrincipalAssignmentRepository>();
        
        services.AddScoped<IAuthorizationStrategy, TenantUserAuthorizationStrategy>();
        services.AddScoped<IAuthorizationStrategy, PrincipalUserAuthorizationStrategy>();
        services.AddScoped<IPolicyEvaluationEngine, PolicyEvaluationEngine>();
        
        services.AddScoped<IPolicyEvaluatorService, PolicyEvaluatorService>();
        services.AddScoped<PolicyEvaluatorService>();
        services.AddScoped<ConditionEvaluatorService>();
        services.AddScoped<IJwtProvider, Auth.JwtProvider>();
        services.AddScoped<IDeviceProvider, Auth.DeviceProvider>();
        services.AddScoped<IPasswordHasher, Auth.PasswordHasher>();
        services.AddScoped<ICurrentTenantUserRepository, Auth.CurrentTenantUserRepository>();
        services.AddScoped<IAuthorizationContextFactory,AuthorizationContextFactory>();
        services.AddScoped<IDeviceSignatureVerifier,  DeviceSignatureVerifier>();
        services.AddScoped<IPublicKeyProvider, PublicKeyProvider>();
        services.AddSingleton<PrincipalLoginService>();
        services.AddMemoryCache();
        services.Decorate<IPublicKeyProvider, CachePublicKeyProvider>();
        services.Scan(scan => scan.FromAssemblyOf<IOperationEvaluator>()
        .AddClasses(classes => classes.AssignableTo<IOperationEvaluator>())
        .AsImplementedInterfaces()
        .WithScopedLifetime());
        services.AddScoped<IConditionRepository, ConditionRepository>();
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
