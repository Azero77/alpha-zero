using ErrorOr;

namespace AlphaZero.Shared.Authorization;

/// <summary>
/// Provides methods for evaluating authorization policies and determining whether a principal has the required
/// permissions to access a specified resource.
/// </summary>
public interface IPolicyEvaluatorService
{
    Task<ErrorOr<Success>> Authorize(AuthorizationContext context);
}
