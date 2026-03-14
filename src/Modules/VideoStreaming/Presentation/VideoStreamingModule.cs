using Autofac;
using MediatR;

namespace Presentation;

public class VideoStreamingModule : Module,IModule
{
    public Task Register(ContainerBuilder services)
    {
        throw new NotImplementedException();
    }

    public Task<TResponse> Send<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) where TRequest : IRequest<TResponse>
    {
        throw new NotImplementedException();
    }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);
    }
}
