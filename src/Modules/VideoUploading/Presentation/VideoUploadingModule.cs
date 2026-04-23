using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Infrastructure;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using Autofac;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Presentation;

public class VideoUploadingModule : AppModule, IVideoUploadingModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
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
        configuration.AddSagaStateMachine<VideoUploadingSaga, VideoState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<AppDbContext>();
            r.UsePostgres();
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
        });

        // Register local consumers that should run on the in-memory bus with their own scopes
        configuration.AddConsumers(typeof(VideoUploadingModule).Assembly);
        configuration.AddConsumers(typeof(AppDbContext).Assembly);
    }
}
