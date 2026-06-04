using AlphaZero.Modules.Assessments.Application;
using AlphaZero.Modules.Assessments.Infrastructure;
using AlphaZero.Modules.Assessments.Infrastructure.Persistence;
using Autofac;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Assessments.Presentation;

public class AssessmentsModule : AppModule, IAssessmentsModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        if (Configuration is not null)
            globalServices.AddAssessmentsGlobalInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Assessments Module");

        globalServices.AddSingleton<IAssessmentsModule>(this);
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        if (Configuration is not null)
            moduleServices.AddAssessmentsPrivateInfrastructure(Configuration);
        else
            _logger?.LogWarning("Configuration is null in Assessments Module (Private)");
    }

    public override void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        configuration.AddConsumers(typeof(AssessmentsModule).Assembly);
        
        // Add Sagas if needed later
    }
}