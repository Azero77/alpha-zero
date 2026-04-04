using AlphaZero.API.Shared;
using Amazon.S3;
using Application;
using Autofac;
using Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Presentation;


public class VideoStreamingModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        // Add any global infrastructure if needed
        globalServices.AddSingleton<VideoStreamingModule>();
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration == null) _logger.LogWarning("Configuration in VideoStreaming are not found");
        else
            moduleServices.AddVideoStreamingInfrastructure(Configuration);

        // Register all IEndpoint implementations in this assembly
        builder.RegisterAssemblyTypes(this.GetType().Assembly)
               .Where(t => typeof(IEndpoint).IsAssignableFrom(t))
               .AsSelf()
               .InstancePerLifetimeScope();
    }
}
