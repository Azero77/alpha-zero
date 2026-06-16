using AlphaZero.Shared.Domain;
using ErrorOr;
using MassTransit.Middleware;

namespace AlphaZero.Shared.Authorization
{
    public interface IAuthorizationContextFactory
    {
        AuthorizationContext? CurrentAuthorizationContext { get; }

        Task<ErrorOr<AuthorizationContext>> Create(string requiredPermission,ResourceArn arn, AuthenticationMethod authenticationMethod, string id, CancellationToken token = default);
    }
}