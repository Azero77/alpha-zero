using AlphaZero.Modules.Tenants.Infrastructure;
using AlphaZero.Shared.Presentation;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Tenants.Presentation;

public class TenantsModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        if (Configuration is not null)
            globalServices.AddTenantsGlobalInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Tenants Module");
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddTenantsPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Tenants Module (Private)");
    }
}
