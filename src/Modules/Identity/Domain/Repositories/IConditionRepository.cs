using AlphaZero.Modules.Identity.Domain.Models;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IConditionRepository
{
    public Task<IConditionNode?> GetNodeByConditionReferenceName(string name);
}
