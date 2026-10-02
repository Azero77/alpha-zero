using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IConditionRepository
{
    public Task<IConditionNode?> GetNodeByConditionReferenceName(string name);
}
