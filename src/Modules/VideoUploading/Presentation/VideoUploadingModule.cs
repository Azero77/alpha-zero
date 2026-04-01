using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Infrastructure;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using Autofac;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.VideoUploading.Presentation;

public class VideoUploadingModule : AppModule, IVideoUploadingModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        globalServices.AddVideoUploadingGlobalInfrastructure(Configuration ?? 
            throw new ArgumentException("Configuration not found"));
        
        globalServices.AddSingleton<IVideoUploadingModule>(this);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        moduleServices.AddVideoUploadingPrivateInfrastructure(Configuration ??
            throw new ArgumentException("Configuration not found"));
    }

    public override Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (Scope is null) throw new NotImplementedException("Module not initialized");
        var mediatr = Scope.Resolve<IMediator>();
        return mediatr.Send((IRequest<TResponse>)request, cancellationToken);
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
    }
}
