using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Modules.Identity.Infrastructure.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Serialization;
using AlphaZero.Shared.Infrastructure.Caching;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Infrastructure.Models;

public class ConditionDefinition
{
    public required string Name { get; set; }
    public IConditionNode InnerCondition { get; set; } = null!;
}


public class ConditionRepository : IConditionRepository
{
    private static readonly CachePolicy Policy = new(TimeSpan.FromMinutes(10));

    private readonly AppDbContext _context;
    private readonly ICachingService _cache;

    public ConditionRepository(AppDbContext context, ICachingService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IConditionNode?> GetNodeByConditionReferenceName(string name)
    {
        // HybridCache serialises even L1 entries; IConditionNode is polymorphic and only
        // round-trips through the shared converter, so cache its JSON representation.
        var json = await _cache.GetOrCreateAsync(
            IdentityCacheKeys.ConditionDefinition(name),
            Policy,
            [],
            async token =>
            {
                var item = await _context.ConditionDefinitions
                   .FindAsync(new object?[] { name }, token);

                return item?.InnerCondition is { } node
                    ? JsonSerializer.Serialize(node, IdentityCacheJson.Options)
                    : null;
            });

        return json is null ? null : JsonSerializer.Deserialize<IConditionNode>(json, IdentityCacheJson.Options);
    }
}
