using AlphaZero.Modules.Library.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Library.Infrastructure;

public class StrategyFactory(IServiceProvider serviceProvider) : IRedemptionStrategyFactory
{
    public IRedemptionStrategy GetStrategy(string strategyId)
    {
        var strategies = serviceProvider.GetServices<IRedemptionStrategy>();
        var strategy = strategies.FirstOrDefault(s => s.StrategyId == strategyId);

        if (strategy == null)
        {
            throw new NotSupportedException($"Activation strategy '{strategyId}' is not supported.");
        }

        return strategy;
    }

    public IRevocationStrategy GetRevocationStrategy(string strategyId)
    {
        var strategies = serviceProvider.GetServices<IRevocationStrategy>();
        var strategy = strategies.FirstOrDefault(s => s.StrategyId == strategyId);

        if (strategy == null)
        {
            throw new NotSupportedException($"Revocation strategy '{strategyId}' is not supported.");
        }

        return strategy;
    }
}
