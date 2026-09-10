using AlphaZero.Modules.Documents.Application;
using AlphaZero.Modules.Documents.Infrastructure;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using Autofac;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Presentation;

public class DocumentsModule : AppModule, IDocumentsModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        if (Configuration is not null)
            globalServices.AddDocumentsGlobalInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Documents Module");

        globalServices.AddSingleton<IDocumentsModule>(this);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddDocumentsPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Documents Module (Private)");
    }

    public override void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        configuration.AddConsumers(typeof(DocumentsModule).Assembly);
        configuration.AddConsumers(typeof(AppDbContext).Assembly);
    }
}
