using AlphaZero.Shared.Domain;
using ErrorOr;
using MassTransit.Middleware;

namespace AlphaZero.Shared.Authorization
{
    public interface IAuthorizationContextProvider
    {
        ErrorOr<AuthorizationContext?> Get(CancellationToken token = default);
    }
}