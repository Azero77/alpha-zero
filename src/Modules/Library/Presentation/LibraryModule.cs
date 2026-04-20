using AlphaZero.Modules.Library.Infrastructure;
using AlphaZero.Shared.Presentation;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Presentation;

public class LibraryModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        if (Configuration is not null)
            globalServices.AddLibraryGlobalInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Library Module");
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddLibraryPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Library Module (Private)");
    }
}
