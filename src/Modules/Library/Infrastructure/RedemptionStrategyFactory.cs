using AlphaZero.Modules.Library.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Modules.Library.Infrastructure;

public class RedemptionStrategyFactory : IRedemptionStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public RedemptionStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IRedemptionStrategy GetStrategy(string strategyId)
    {
        var strategies = _serviceProvider.GetServices<IRedemptionStrategy>();
        var strategy = strategies.FirstOrDefault(s => s.StrategyId == strategyId);

        if (strategy == null)
        {
            throw new NotSupportedException($"Strategy '{strategyId}' is not supported.");
        }

        return strategy;
    }
}
