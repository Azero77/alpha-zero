using AlphaZero.Shared.Presentation;
using Autofac;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Identity;

/// <summary>
/// This is the module's entry point for the dependency injection engine.
/// It's used to register our services to the global or private container.
/// </summary>
public class IdentityModule : AppModule
{
    public override void RegisterGlobal(IServiceCollection globalServices)
    {
        // 1. Services shared with other modules (e.g. Identity Shared Contract if it existed)
    }

    public override void RegisterPrivate(IServiceCollection moduleServices, ContainerBuilder builder)
    {
        // 2. Services internal to this module (e.g. IUserRepository -> UserRepository)
    }

    public override void Initialize(ILifetimeScope scope)
    {
        // 3. One-time setup after everything is wired up
    }
}
