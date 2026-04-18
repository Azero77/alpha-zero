using System.Text.Json;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

public interface IRedemptionStrategy
{
    string StrategyId { get; }
    Task ExecuteAsync(Guid userId, ResourceArn resource, JsonElement metadata);
}
