using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

public interface IRevocationStrategy
{
    string StrategyId { get; }
    Task ExecuteAsync(Guid userId, Guid accessCodeId, ResourceArn resource);
}
