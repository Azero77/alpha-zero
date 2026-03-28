using Autofac;
using Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation;

public class CoursesModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        globalServices.AddCoursesGlobalInfrastructure(Configuration ?? 
        throw new ArgumentException("Configuration in Courses are not found")
            );
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        moduleServices.AddCoursesPrivateInfrastructure(Configuration ??
        throw new ArgumentException("Configuration in Courses are not found")
            );
    }

    public override Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request, CancellationToken token = default)
    {

        if (Scope is null) throw new NotImplementedException("Container not implemented");
        var mediatr = Scope.Resolve<IMediator>();
        return mediatr.Send(request,token);
    }
}
