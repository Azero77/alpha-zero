using Application;
using Autofac;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Presentation;

public class VideoStreamingModule : AppModule
{
    public override void Register(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        moduleServices.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(IVideoStreamingApplicationMarker).Assembly));
    }

    public override Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request)
    {
        if (Scope is null) throw new NotImplementedException("Container not implemented");
        var mediatr = Scope.Resolve<IMediator>();
        return mediatr.Send(request);
    }
}
