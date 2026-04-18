namespace AlphaZero.Modules.Library.Domain;

public interface IRedemptionStrategyFactory
{
    IRedemptionStrategy GetStrategy(string strategyId);
}
