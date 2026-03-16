using Autofac;
using Autofac.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public interface IModule
{
    Task<TResponse> Send<TRequest,TResponse>(IRequest<TResponse> request)
        where TRequest : IRequest<TResponse>;
    void RegisterPrivate(IServiceCollection services, ContainerBuilder builder);
    void RegisterGlobal(IServiceCollection services);

    void Initialize(ILifetimeScope scope);

    IConfiguration? Configuration { get; set; }
}

/// <summary>
/// Abstracts Autofac containers into using MSDI
/// </summary>
public abstract class AppModule : Module, IModule
{
    protected ILifetimeScope? Scope { get; private set; }
    public IConfiguration? Configuration { get; set; }

    public virtual void Initialize(ILifetimeScope root)
    {
        Scope = root.BeginLifetimeScope(builder =>
        {
            ServiceCollection services = new();
            RegisterPrivate(services, builder);
            builder.Populate(services);
        });
    }
    public abstract Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request) where TRequest : IRequest<TResponse>;

    public abstract void RegisterPrivate(IServiceCollection services, ContainerBuilder builder);

    public abstract void RegisterGlobal(IServiceCollection services);
}
