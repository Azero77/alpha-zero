using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using System.Text.Json;

namespace AlphaZero.Shared.Infrastructure.Repositores;

public class CachingRepository<TContext, TEntity> : BaseRepository<TContext, TEntity>
    where TContext : DbContext
    where TEntity : Entity
{
    private readonly BaseRepository<TContext, TEntity> _innerRepository;
    private readonly HybridCache _cache;
    public CachingRepository(TContext context, BaseRepository<TContext, TEntity> innerRepository, HybridCache cache) : base(context)
    {
        _innerRepository = innerRepository;
        _cache = cache;
    }

    public override async Task<TEntity?> GetById(Guid id, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(
            key: $"{typeof(TEntity).Name}:{id}",
            async token => await _innerRepository.GetById(id, token),
            cancellationToken : ct
            );
    }
}