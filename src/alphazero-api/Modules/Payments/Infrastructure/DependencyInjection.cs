using AlphaZero.Modules.Payments.Application.Catalog;
using AlphaZero.Modules.Payments.Application.Checkout;
using AlphaZero.Modules.Payments.Application.Settings;
using AlphaZero.Modules.Payments.Application.Purchases;
using AlphaZero.Modules.Payments.Application.Queries;
using AlphaZero.Modules.Payments.Application.Webhooks;
using AlphaZero.Modules.Payments.Infrastructure.CrossModule;
using AlphaZero.Modules.Payments.Infrastructure.Behaviors;
using AlphaZero.Modules.Payments.Infrastructure.Persistence;
using AlphaZero.Modules.Payments.Infrastructure.Webhooks;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DatabaseSettings dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__PaymentsMigrationHistory", AppDbContext.Schema);
            });
        });
        services.AddHostedService<WebhookSweeperWorker>();

        return services;
    }

    public static IServiceCollection AddPaymentsPrivateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
        services.AddScoped<ICatalogProductRepository, CatalogProductRepository>();
        services.AddScoped<IPriceRepository, CatalogPriceRepository>();
        services.AddScoped<IPaymentConfigurationRepository, PaymentConfigurationRepository>();
        services.AddScoped<IPurchaseCaptureRepository, PurchaseCaptureRepository>();
        services.AddScoped<IPurchaseCaptureUnitOfWork, PurchaseCaptureUnitOfWork>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();
        services.AddScoped<ICheckoutSessionLifecycleRepository>(sp => sp.GetRequiredService<CheckoutRepository>());
        services.AddScoped<ICourseAccessReader, BusCourseAccessReader>();
        services.AddScoped<IPaymentsReadStore, PaymentsReadStore>();
        services.AddScoped<IWebhookStore, WebhookStore>();
        services.AddScoped<IProviderEventNormalizerRegistry, ProviderEventNormalizerRegistry>();
        services.AddScoped<CoursePlanAvailabilityConsumer>();

        // Register MediatR handlers and validators from the Application assembly
        var applicationAssembly = typeof(Application.IPaymentsModule).Assembly;

        services.AddValidatorsFromAssembly(applicationAssembly);

        services.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(applicationAssembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(TransactionalBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        return services;
    }
}
