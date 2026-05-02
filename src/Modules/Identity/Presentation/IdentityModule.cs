using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Infrastructure;
using AlphaZero.Shared.Presentation;
using Autofac;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Presentation;

public class IdentityModule : AppModule, IIdentityModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        if (Configuration is not null)
            globalServices.AddIdentityGlobalInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Identity Module");

        globalServices.AddSingleton<IIdentityModule>(this);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddIdentityPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Identity Module (Private)");
    }

    public override void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        configuration.AddConsumers(typeof(IdentityModule).Assembly);
    }
}
