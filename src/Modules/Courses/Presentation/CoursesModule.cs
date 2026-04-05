using Autofac;
using Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Presentation;

public class CoursesModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        if (Configuration is not null)
            globalServices.AddCoursesGlobalInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Courses Module");
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddCoursesPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Courses Module (Private)");
    }
}
