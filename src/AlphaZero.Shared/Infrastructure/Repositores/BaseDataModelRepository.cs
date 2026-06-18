using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace AlphaZero.Shared.Infrastructure.Repositores;
/// <summary>
/// Repository base for modules that separate Data Models (EF) from Domain Models.
/// Implements IRepository&lt;TDomainModel&gt; so consumers see only the domain type.
/// Internally maps via IDataModelMapper and tracks modifications in a local list,
/// flushed by UnitOfWork before SaveChanges.
/// </summary>
public class BaseDataModelRepository<TContext, TDomainModel, TDataModel>
    : IRepository<TDomainModel>
    where TContext : DbContext
    where TDomainModel : Entity
    where TDataModel : class
{
    protected readonly TContext _context;
    protected readonly IDataModelMapper<TDomainModel, TDataModel> _mapper;
    private readonly ILogger<BaseDataModelRepository<TContext, TDomainModel, TDataModel>> _logger;
    protected IUnitOfWork _unitOfWork;

    public BaseDataModelRepository(TContext context, IDataModelMapper<TDomainModel, TDataModel> mapper, IUnitOfWork unitOfWork, ILogger<BaseDataModelRepository<TContext, TDomainModel, TDataModel>> logger)
    {
        _context = context;
        _mapper = mapper;
        this._unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // IRepository<TDomainModel> — write operations
    // ──────────────────────────────────────────────────────────────

    public virtual void Add(TDomainModel entity)
    {
        var dataModel = _mapper.ToData(entity);
        _context.Set<TDataModel>().Add(dataModel);
        TrackEntity(entity);
    }

    public virtual void Update(TDomainModel entity)
    {
        var dataModel = _mapper.ToData(entity);
        _context.Set<TDataModel>().Update(dataModel);
        TrackEntity(entity);
    }

    public virtual void Remove(TDomainModel entity)
    {
        var dataModel = _mapper.ToData(entity);
        _context.Set<TDataModel>().Remove(dataModel);
        TrackEntity(entity);
    }

    // ──────────────────────────────────────────────────────────────
    // IRepository<TDomainModel> — read operations
    // ──────────────────────────────────────────────────────────────

    public IQueryable<TDomainModel> Entities =>
        throw new NotSupportedException(
            "Direct IQueryable is not supported on DataModel repositories. " +
            "Use the typed query methods instead.");

    public virtual async Task<TDomainModel?> GetById(Guid id, CancellationToken token = default)
    {
        var dataModel = await _context.Set<TDataModel>().FindAsync([id], token);
        if (dataModel is null) return null;

        var domain = _mapper.ToDomain(dataModel);
        TrackEntity(domain);
        return domain;
    }

    public virtual async Task<IReadOnlyCollection<TDomainModel>> GetAll(CancellationToken token = default)
    {
        var dataModels = await _context.Set<TDataModel>()
            .AsNoTracking()
            .ToListAsync(token);

        return dataModels.Select(d =>
        {
            var domain = _mapper.ToDomain(d);
            TrackEntity(domain);
            return domain;
        }).ToList();
    }

    public virtual async Task<IReadOnlyCollection<TDomainModel>> Get(
        Expression<Func<TDomainModel, bool>> filter, CancellationToken token = default)
    {
        // For DataModel repos, the filter is compiled against domain models
        // after materialization. Subclasses should override with a DataModel-aware filter for perf.
        var all = await GetAll(token);
        var compiled = filter.Compile();
        _logger.LogCritical("BaseDataModelRepository.Get() is using a domain-level filter on an in-memory collection. " +
            "This is inefficient and should be overridden in a subclass with a DataModel-aware filter.");
        return all.Where(compiled).ToList();
    }

    public virtual async Task<TDomainModel?> GetFirst(
        Expression<Func<TDomainModel, bool>> filter, CancellationToken token = default)
    {
        var results = await Get(filter, token);
        return results.FirstOrDefault();
    }

    public virtual async Task<bool> Any(
        Expression<Func<TDomainModel, bool>> filter, CancellationToken token = default)
    {
        var results = await Get(filter, token);
        return results.Any();
    }

    public virtual async Task<int> Count(
        Expression<Func<TDomainModel, bool>>? filter = null, CancellationToken token = default)
    {
        if (filter is null)
            return await _context.Set<TDataModel>().CountAsync(token);

        var results = await Get(filter, token);
        return results.Count;
    }

    public virtual async Task<PagedResult<TDomainModel>> Get<TKey>(
        int pageNumber, int perPage,
        Expression<Func<TDomainModel, TKey>> orderBy,
        bool ascending = true, CancellationToken token = default)
    {
        var all = await GetAll(token);
        var ordered = ascending
            ? all.AsQueryable().OrderBy(orderBy)
            : all.AsQueryable().OrderByDescending(orderBy);
        _logger.LogCritical("BaseDataModelRepository.Get() is using a domain-level filter on an in-memory collection. " +
            "This is inefficient and should be overridden in a subclass with a DataModel-aware filter.");
        var count = all.Count;
        var page = ordered.Skip((pageNumber - 1) * perPage).Take(perPage).ToList();
        return new PagedResult<TDomainModel>(page, count, pageNumber, perPage);
    }

    public virtual async Task<PagedResult<TDomainModel>> Get<TKey>(
        int pageNumber, int perPage,
        Expression<Func<TDomainModel, bool>> filter,
        Expression<Func<TDomainModel, TKey>> orderBy,
        bool ascending = true, CancellationToken token = default)
    {
        var filtered = await Get(filter, token);
        var ordered = ascending
            ? filtered.AsQueryable().OrderBy(orderBy)
            : filtered.AsQueryable().OrderByDescending(orderBy);

        var count = filtered.Count;
        var page = ordered.Skip((pageNumber - 1) * perPage).Take(perPage).ToList();
        return new PagedResult<TDomainModel>(page, count, pageNumber, perPage);
    }

    private void TrackEntity(TDomainModel model)
    {
        if (model is AggregateRoot aggregateRoot)
            _unitOfWork.TrackEntity(aggregateRoot);
    }
}
