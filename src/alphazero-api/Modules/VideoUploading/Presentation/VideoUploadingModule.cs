using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Infrastructure;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Presentation.Services;
using AlphaZero.Shared.Application;
using Autofac;
using MassTransit;
using AlphaZero.Shared.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Presentation;

public class VideoUploadingModule : AppModule, IVideoUploadingModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        globalServices.AddSignalR();
        globalServices.AddScoped<IVideoProgressNotifier, SignalRVideoProgressNotifier>();

        if (Configuration is not null)
            globalServices.AddVideoUploadingGlobalInfrastructure(Configuration);
        else
            _logger.LogWarning("Configuration is null in VideoUploading Module");
        globalServices.AddSingleton<IVideoUploadingModule>(this);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddVideoUploadingPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in VideoUploading Module (Private)");
    }

    public override void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        configuration.AddEntityFrameworkOutbox<AlphaZero.Modules.VideoUploading.Infrastructure.Persistance.AppDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromMinutes(5);
        });

        // Register local consumers that should run on the in-memory bus with their own scopes (SQS consumers are registered on IExternalBus)
        configuration.AddModuleConsumers<Infrastructure.Persistance.AppDbContext>(filter => !filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), typeof(VideoUploadingModule).Assembly);
        configuration.AddModuleConsumers<Infrastructure.Persistance.AppDbContext>(filter => !filter.Name.Contains("sqs", StringComparison.InvariantCultureIgnoreCase), typeof(Infrastructure.Persistance.AppDbContext).Assembly);
    }
}
