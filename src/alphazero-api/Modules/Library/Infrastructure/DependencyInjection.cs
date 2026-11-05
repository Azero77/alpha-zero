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

        // Scheduled Library analytics/sweep worker (issue #59, Courses template): sweep expired
        // codes through the state machine, then refresh the LibraryAnalytics projection.
        services.AddHostedService<Analytics.LibraryAnalyticsRefreshWorker>();

        return services;
    }

    public static IServiceCollection AddLibraryPrivateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccessCodeRepository, AccessCodeRepository>();
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<IRedemptionAuditLogRepository, RedemptionAuditLogRepository>();
        services.AddScoped<ISaleRecordRepository, SaleRecordRepository>();
        services.AddScoped<ILibraryAuditLogRepository, LibraryAuditLogRepository>();
        services.AddScoped<AlphaZero.Modules.Library.Application.Queries.ILibraryQueryService, AlphaZero.Modules.Library.Infrastructure.Queries.LibraryQueryService>();
        services.Decorate<AlphaZero.Modules.Library.Application.Queries.ILibraryQueryService, AlphaZero.Modules.Library.Infrastructure.Caching.CachedLibraryQueryService>();
        services.AddScoped<AlphaZero.Modules.Library.Application.Analytics.ILibraryAnalyticsQueryService, AlphaZero.Modules.Library.Infrastructure.Queries.LibraryAnalyticsQueryService>();
        services.Decorate<AlphaZero.Modules.Library.Application.Analytics.ILibraryAnalyticsQueryService, AlphaZero.Modules.Library.Infrastructure.Caching.CachedLibraryAnalyticsQueryService>();
        services.AddScoped<AlphaZero.Modules.Library.Application.Analytics.IProvisioningStateReadService, AlphaZero.Modules.Library.Infrastructure.CrossModule.BusProvisioningStateService>();

        // Analytics worker services (issue #59): resolved in the module's own container so their
        // IUnitOfWork / AppDbContext are unambiguously the Library ones; the hosted worker bridges
        // host lifecycle → module scope per pass (Courses template).
        services.AddScoped<AlphaZero.Modules.Library.Infrastructure.Analytics.ILibraryAnalyticsRefreshService, AlphaZero.Modules.Library.Infrastructure.Analytics.LibraryAnalyticsRefreshService>();
        services.AddScoped<AlphaZero.Modules.Library.Infrastructure.Analytics.IAccessCodeExpirySweepService, AlphaZero.Modules.Library.Infrastructure.Analytics.AccessCodeExpirySweepService>();

        services.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();

        // Register MediatR handlers and validators from the Application assembly
        var applicationAssembly = typeof(AlphaZero.Modules.Library.Application.RedeemCode.RedeemCodeCommand).Assembly;
        
        services.AddValidatorsFromAssembly(applicationAssembly);

        services.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(applicationAssembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(AlphaZero.Modules.Library.Infrastructure.Behaviors.TransactionalBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        return services;
    }
}
