using Autofac;
using Autofac.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

public interface IModule
{
    Task<TResponse> Send<TRequest,TResponse>(IRequest<TResponse> request)
        where TRequest : IRequest<TResponse>;
    void Register(IServiceCollection services, ContainerBuilder builder);

    void Initialize(ILifetimeScope scope);
}

/// <summary>
/// Abstracts Autofac containers into using MSDI
/// </summary>
public abstract class AppModule : Module, IModule
{
    protected ILifetimeScope? Scope { get; private set; }
    public void Initialize(ILifetimeScope root)
    {
        Scope = root.BeginLifetimeScope(b => b.RegisterModule(this));
    }

    public abstract void Register(IServiceCollection moduleServices,ContainerBuilder builder);

    public abstract Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request) where TRequest : IRequest<TResponse>;
    protected override void Load(ContainerBuilder builder)
    {
        ServiceCollection services = new();
        Register(services,builder);
        builder.Populate(services);
    }

}