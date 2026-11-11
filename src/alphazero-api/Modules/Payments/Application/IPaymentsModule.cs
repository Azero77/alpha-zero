namespace AlphaZero.Modules.Payments.Application;

/// <summary>
/// Application-owned module handle. Lets infrastructure components resolve scoped services
/// from the Payments module's own container (mirrors <c>ILibraryModule</c>).
/// </summary>
public interface IPaymentsModule : IModule
{
}
