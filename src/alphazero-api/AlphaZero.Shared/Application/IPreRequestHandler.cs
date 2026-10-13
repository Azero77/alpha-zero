using AlphaZero.Shared.Authorization;
using ErrorOr;

namespace AlphaZero.Shared.Application;

public interface IPreRequestHandler<in TRequest>
{
    Task<ErrorOr<Success>> PreHandle(
        TRequest request,
        AuthorizationContext? context,
        CancellationToken cancellationToken);
}
