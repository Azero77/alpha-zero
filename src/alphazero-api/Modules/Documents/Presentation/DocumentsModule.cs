using AlphaZero.Modules.Documents.Application;
using AlphaZero.Modules.Documents.Infrastructure;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using AlphaZero.Shared.Application;
using Autofac;
using MassTransit;
using AlphaZero.Shared.Infrastructure;
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
        configuration.AddEntityFrameworkOutbox<AlphaZero.Modules.Documents.Infrastructure.Persistance.AppDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromMinutes(5);
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
        });

        configuration.AddModuleConsumers<Infrastructure.Persistance.AppDbContext>(typeof(DocumentsModule).Assembly);
        configuration.AddModuleConsumers<Infrastructure.Persistance.AppDbContext>(typeof(Infrastructure.Persistance.AppDbContext).Assembly);
    }
}
