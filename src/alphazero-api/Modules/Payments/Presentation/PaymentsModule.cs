using AlphaZero.Modules.Payments.Infrastructure;
using AlphaZero.Modules.Payments.Infrastructure.Persistence;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Migrations;
using AlphaZero.Modules.Payments.Infrastructure.Consumers;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Presentation;
using Autofac;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using AlphaZero.Shared.Infrastructure;

namespace AlphaZero.Modules.Payments.Presentation;

public class PaymentsModule : AppModule, AlphaZero.Modules.Payments.Application.IPaymentsModule
{
    public override Task RunMigrations() => PaymentsSqlMigrationRunner.ApplyConfiguredAsync(
        Configuration!,
        allowRuntimeConnectionFallback: true);

    // The module must never run with degraded persistence wiring: without its
    // persistence configuration the failure would otherwise be deferred to the
    // first request. Configuration is null exactly when the application wiring
    // fails to pass it in - abort module startup, naming the missing section.
    private void ThrowIfConfigurationMissing(string registrationStage)
    {
        if (Configuration is null)
        {
            throw new InvalidOperationException(
                $"The Payments module cannot start: persistence configuration is missing " +
                $"(configuration section \"DatabaseSettings\" was not resolved for the module, {registrationStage}). " +
                "No module may run with degraded persistence wiring.");
        }
    }

    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        ThrowIfConfigurationMissing(nameof(RegisterGlobal));
        globalServices.AddPaymentsGlobalInfrastructure(Configuration!);
        globalServices.AddSingleton<AlphaZero.Modules.Payments.Application.IPaymentsModule>(this);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        ThrowIfConfigurationMissing(nameof(RegisterPrivate));
        moduleServices.AddPaymentsPrivateInfrastructure(Configuration!);
    }

    public override void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        configuration.AddConsumer<CoursePlanAvailabilityChangedConsumer>();
        configuration.AddConsumer<ProcessWebhookEventConsumer>();
        configuration.AddEntityFrameworkOutbox<AppDbContext>(o =>
        {
            o.UsePostgres(false);
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromSeconds(5);
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
        });

        configuration.AddModuleConsumers<AppDbContext>(typeof(PaymentsModule).Assembly);
    }
}
