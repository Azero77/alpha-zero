using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

public interface IModule
{
    Task<TResponse> Send<TRequest,TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>;

    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    void RegisterPrivate(IServiceCollection services, ContainerBuilder builder);
    void RegisterGlobal(IServiceCollection services);

    void Initialize(ILifetimeScope scope);
    void ConfigureModuleBus(IBusRegistrationConfigurator configuration);
    IConfiguration? Configuration { get; set; }
}

/// <summary>
/// Abstracts Autofac containers into using MSDI
/// </summary>
public abstract class AppModule : Module, IModule
{
    protected ILifetimeScope? Scope { get; private set; }
    public IConfiguration? Configuration { get; set; }
    protected ILogger<AppModule> _logger { get; private set; }

    public virtual void Initialize(ILifetimeScope root)
    {
        Scope = root.BeginLifetimeScope(builder =>
        {
            ServiceCollection services = new();
            RegisterPrivate(services, builder);
            builder.Populate(services);
        });

        _logger = Scope.Resolve<ILogger<AppModule>>();
    }

    public virtual async Task<TResponse> Send<TRequest, TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse>
    {
        if (Scope is null) throw new InvalidOperationException("Module not initialized. Did you forget to call Initialize()?");
        
        using var requestScope = Scope.BeginLifetimeScope();
        var mediatr = requestScope.Resolve<IMediator>();
        return await mediatr.Send(request, cancellationToken);
    }

    public virtual async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (Scope is null) throw new InvalidOperationException("Module not initialized. Did you forget to call Initialize()?");

        using var requestScope = Scope.BeginLifetimeScope();
        var mediatr = requestScope.Resolve<IMediator>();
        return await mediatr.Send(request, cancellationToken);
    }

    public abstract void RegisterPrivate(IServiceCollection services, ContainerBuilder builder);

    public abstract void RegisterGlobal(IServiceCollection services);
    public virtual void ConfigureModuleBus(IBusRegistrationConfigurator configuration)
    {
        return;
    }

    public async Task RunMigrations()
    {
        if (Scope is null) return;

        // Find the module's base namespace (e.g., AlphaZero.Modules.VideoUploading)
        var moduleNamespace = this.GetType().Namespace;
        if (string.IsNullOrEmpty(moduleNamespace)) return;

        var parts = moduleNamespace.Split('.');
        if (parts.Length < 3) return;
        var baseNamespace = string.Join(".", parts.Take(3));

        // Search for a type named 'AppDbContext' in assemblies starting with the base namespace
        var dbContextType = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName != null && a.FullName.StartsWith(baseNamespace))
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == "AppDbContext" && typeof(DbContext).IsAssignableFrom(t));

        if (dbContextType is null) return;

        // Resolve the found DbContext type from the scope
        using var migrationScope = Scope.BeginLifetimeScope();
        if (migrationScope.Resolve(dbContextType) is DbContext db)
        {
            /*var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await db.Database.MigrateAsync();
            }*/
            await db.Database.MigrateAsync();
        }
    }
}
