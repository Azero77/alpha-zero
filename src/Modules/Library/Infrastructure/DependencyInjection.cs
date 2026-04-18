using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Modules.Library.Infrastructure.Repositories;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Library.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLibraryGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__LibraryMigrationHistory", AppDbContext.Schema);
            });
            opts.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });

        return services;
    }

    public static IServiceCollection AddLibraryPrivateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccessCodeRepository, AccessCodeRepository>();
        services.AddScoped<IRedemptionStrategyFactory, RedemptionStrategyFactory>();
        
        // Register Redemption Strategies (ACLs)
        services.AddScoped<IRedemptionStrategy, CourseEnrollmentStrategy>();

        services.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();

        // Register MediatR handlers from the Application assembly
        var applicationAssembly = typeof(AlphaZero.Modules.Library.Application.RedeemCode.RedeemCodeCommand).Assembly;
        
        services.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(applicationAssembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        return services;
    }
}
