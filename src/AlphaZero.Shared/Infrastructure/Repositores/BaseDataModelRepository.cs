using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using Castle.Core.Logging;
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
public abstract class BaseDataModelRepository<TContext, TDomainModel, TDataModel>
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

    public virtual async Task<TDomainModel?> GetById(Guid id, CancellationToken token = default)
    {
        var dataModel = await _context.Set<TDataModel>().FindAsync([id], token);
        if (dataModel is null) return null;

        var domain = _mapper.ToDomain(dataModel);
        TrackEntity(domain);
        return domain;
    }

    public abstract Task<TDomainModel?> GetFirst(
        Expression<Func<TDomainModel, bool>> filter, CancellationToken token = default);
    

    public virtual async Task<bool> Any(
        Expression<Func<TDomainModel, bool>> filter, CancellationToken token = default)
    {
        var dataModels = await _context.Set<TDataModel>().AsNoTracking().ToListAsync(token);
        var compiled = filter.Compile();
        return dataModels.Select(d => _mapper.ToDomain(d)).Any(compiled);
    }

    public virtual async Task<int> Count(
        Expression<Func<TDomainModel, bool>>? filter = null, CancellationToken token = default)
    {
        if (filter is null)
            return await _context.Set<TDataModel>().CountAsync(token);

        var dataModels = await _context.Set<TDataModel>().AsNoTracking().ToListAsync(token);
        var compiled = filter.Compile();
        return dataModels.Select(d => _mapper.ToDomain(d)).Count(compiled);
    }

    private void TrackEntity(TDomainModel model)
    {
        if (model is AggregateRoot aggregateRoot)
            _unitOfWork.TrackEntity(aggregateRoot);
    }
}
