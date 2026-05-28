using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Shared.Authorization
{
    public interface IAuthorizationContextFactory
    {
        Task<ErrorOr<AuthorizationContext>> Create(ResourceArn arn, AuthenticationMethod authenticationMethod, string id);
    }
}