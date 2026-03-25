using AlphaZero.Modules.VideoUploading.Infrastructure;
using Autofac;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.VideoUploading.Presentation;

public class VideoUploadingModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        globalServices.AddVideoUploadingGlobalInfrastructure(Configuration ?? 
            throw new ArgumentException("Configuration not found"));
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        moduleServices.AddVideoUploadingPrivateInfrastructure(Configuration ??
            throw new ArgumentException("Configuration not found"));
    }

    public override Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request)
    {
        if (Scope is null) throw new NotImplementedException("Module not initialized");
        var mediatr = Scope.Resolve<IMediator>();
        return mediatr.Send((IRequest<TResponse>)request);
    }
}
