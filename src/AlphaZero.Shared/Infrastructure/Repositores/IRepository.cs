using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AlphaZero.Shared.Infrastructure.Repositores;

public interface IRepository<TEntity>
    where TEntity : Entity
{
    void Add(TEntity entity);
    void Remove(TEntity entity);
    void Update(TEntity entity);

    Task<IReadOnlyCollection<TEntity>> GetAll(CancellationToken token = default);
    Task<TEntity?> GetFirst(Expression<Func<TEntity, bool>> filter, CancellationToken token = default);
    Task<bool> Any(Expression<Func<TEntity, bool>> filter, CancellationToken token = default);
    Task<int> Count(Expression<Func<TEntity, bool>>? filter = null, CancellationToken token = default);

    Task<PagedResult<TEntity>> Get<TKey>(
        int pageNumber,
        int perPage,
        Expression<Func<TEntity, TKey>> orderBy,
        bool ascending = true,
        CancellationToken token = default);

    Task<PagedResult<TEntity>> Get<TKey>(
        int pageNumber,
        int perPage,
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TKey>> orderBy,
        bool ascending = true,
        CancellationToken token = default);

    Task<TEntity?> GetById(Guid id);
}

public class BaseRepository<TContext, TEntity> : IRepository<TEntity>
    where TContext : DbContext
    where TEntity : Entity
{
    protected readonly TContext _context;

    public BaseRepository(TContext context)
    {
        _context = context;
    }

    public virtual void Add(TEntity entity) => _context.Set<TEntity>().Add(entity);

    public virtual void Remove(TEntity entity) => _context.Set<TEntity>().Remove(entity);

    public virtual void Update(TEntity entity) => _context.Set<TEntity>().Update(entity);

    public async virtual Task<IReadOnlyCollection<TEntity>> GetAll(CancellationToken token = default)
    {
        return await _context.Set<TEntity>()
            .AsNoTracking()
            .ToListAsync(token);
    }

    public async virtual Task<TEntity?> GetFirst(Expression<Func<TEntity, bool>> filter, CancellationToken token = default)
    {
        return await _context.Set<TEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(filter, token);
    }

    public async virtual Task<bool> Any(Expression<Func<TEntity, bool>> filter, CancellationToken token = default)
    {
        return await _context.Set<TEntity>()
            .AnyAsync(filter, token);
    }

    public async virtual Task<int> Count(Expression<Func<TEntity, bool>>? filter = null, CancellationToken token = default)
    {
        var query = _context.Set<TEntity>().AsNoTracking();
        if (filter != null)
        {
            query = query.Where(filter);
        }
        return await query.CountAsync(token);
    }

    public async virtual Task<PagedResult<TEntity>> Get<TKey>(
        int pageNumber,
        int perPage,
        Expression<Func<TEntity, TKey>> orderBy,
        bool ascending = true,
        CancellationToken token = default)
    {
        var query = _context.Set<TEntity>().AsNoTracking();

        var count = await query.CountAsync(token);

        var orderedQuery = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        var response = await orderedQuery
            .Skip((pageNumber - 1) * perPage)
            .Take(perPage)
            .ToListAsync(token);

        return new PagedResult<TEntity>(response, count, pageNumber, perPage);
    }

    public async virtual Task<PagedResult<TEntity>> Get<TKey>(
        int pageNumber,
        int perPage,
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TKey>> orderBy,
        bool ascending = true,
        CancellationToken token = default)
    {
        var query = _context.Set<TEntity>().AsNoTracking().Where(filter);

        var count = await query.CountAsync(token);

        var orderedQuery = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        var response = await orderedQuery
            .Skip((pageNumber - 1) * perPage)
            .Take(perPage)
            .ToListAsync(token);

        return new PagedResult<TEntity>(response, count, pageNumber, perPage);
    }

    public async virtual Task<TEntity?> GetById(Guid id)
    {
        return await _context.Set<TEntity>()
            .FirstOrDefaultAsync(d => d.Id == id);

    }
}