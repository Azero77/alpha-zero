using AlphaZero.Modules.Library.Infrastructure;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Presentation;
using Autofac;
using Microsoft.Extensions.DependencyInjection;

using MassTransit;
using AlphaZero.Shared.Infrastructure;

namespace AlphaZero.Modules.Library.Presentation;

public class LibraryModule : AppModule
{
    // The module must never run with degraded persistence wiring: without its
    // persistence configuration the explosion would otherwise be deferred to the
    // first request. Configuration is null exactly when the application wiring
    // fails to pass it in - abort module startup, naming the missing section.
    private void ThrowIfConfigurationMissing(string registrationStage)
    {
        if (Configuration is null)
        {
            throw new InvalidOperationException(
                $"The Library module cannot start: persistence configuration is missing " +
                $"(configuration section \"DatabaseSettings\" was not resolved for the module, {registrationStage}). " +
                "No module may run with degraded persistence wiring.");
        }
    }

    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        ThrowIfConfigurationMissing(nameof(RegisterGlobal));
        globalServices.AddLibraryGlobalInfrastructure(Configuration);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        ThrowIfConfigurationMissing(nameof(RegisterPrivate));
        moduleServices.AddLibraryPrivateInfrastructure(Configuration);
    }

    public override void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        configuration.AddEntityFrameworkOutbox<AlphaZero.Modules.Library.Infrastructure.Persistance.AppDbContext>(o =>
        {
            o.UsePostgres(false);
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromMinutes(5);
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
        });

        configuration.AddModuleConsumers<Infrastructure.Persistance.AppDbContext>(typeof(LibraryModule).Assembly);
    }
}
