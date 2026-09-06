using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using Microsoft.Extensions.Caching.Hybrid;
using System.Runtime.CompilerServices;

namespace AlphaZero.Modules.Identity.Infrastructure.Models;

public class ConditionDefinition
{
    public required string Name { get; set; }
    public IConditionNode InnerCondition { get; set; } = null!;
}


public class ConditionRepository : IConditionRepository
{
    private readonly AppDbContext _context;
    private readonly HybridCache _hybridCache;

    public ConditionRepository(AppDbContext context, HybridCache hybridCache)
    {
        _context = context;
        _hybridCache = hybridCache;
    }

    public async Task<IConditionNode?> GetNodeByConditionReferenceName(string name)
    {
        return await _hybridCache.GetOrCreateAsync($"ConditionDefinition_{name}",  async (token) =>
        {
            var item = await _context.ConditionDefinitions
               .FindAsync(new { Name = name }, token);

            return item?.InnerCondition;
        });
    }
}