using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AlphaZero.Shared.Infrastructure.Repositores;

/// <summary>
/// Repository base for modules that separate Data Models (EF) from Domain Models.
/// Implements IRepository&lt;TDomainModel&gt; so consumers see only the domain type.
/// Internally maps via IDataModelMapper and tracks modifications in a local list,
/// flushed by UnitOfWork before SaveChanges.
/// </summary>
public class BaseDataModelRepository<TContext, TDomainModel, TDataModel>
    : IRepository<TDomainModel>, ITrackedRepository
    where TContext : DbContext
    where TDomainModel : Entity
    where TDataModel : class
{
    protected readonly TContext _context;
    protected readonly IDataModelMapper<TDomainModel, TDataModel> _mapper;

    // ---------- Change Tracker ----------
    private readonly List<TrackedEntry> _trackedEntries = new();

    // Lookup: DomainModel.Id → (DomainModel, DataModel, State)
    // Keeps the data model around so we can ApplyChanges on flush.
    private readonly Dictionary<Guid, (TDomainModel Domain, TDataModel? Data, TrackedEntryState State)>
        _identityMap = new();

    public BaseDataModelRepository(TContext context, IDataModelMapper<TDomainModel, TDataModel> mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // ──────────────────────────────────────────────────────────────
    // IRepository<TDomainModel> — write operations
    // ──────────────────────────────────────────────────────────────

    public virtual void Add(TDomainModel entity)
    {
        Track(entity, null, TrackedEntryState.Added);
    }

    public virtual void Update(TDomainModel entity)
    {
        if (_identityMap.TryGetValue(entity.Id, out var existing))
        {
            // Already loaded — mark modified (keeps original data model for ApplyChanges)
            _identityMap[entity.Id] = existing with { State = TrackedEntryState.Modified };
            UpdateTrackedEntryState(entity, TrackedEntryState.Modified);
        }
        else
        {
            // Not loaded through this repo — track as modified anyway
            Track(entity, null, TrackedEntryState.Modified);
        }
    }

    public virtual void Remove(TDomainModel entity)
    {
        if (_identityMap.TryGetValue(entity.Id, out var existing))
        {
            _identityMap[entity.Id] = existing with { State = TrackedEntryState.Removed };
            UpdateTrackedEntryState(entity, TrackedEntryState.Removed);
        }
        else
        {
            Track(entity, null, TrackedEntryState.Removed);
        }
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
        // Check identity map first
        if (_identityMap.TryGetValue(id, out var cached)
            && cached.State != TrackedEntryState.Removed)
        {
            return cached.Domain;
        }

        var dataModel = await _context.Set<TDataModel>().FindAsync([id], token);
        if (dataModel is null) return null;

        var domain = _mapper.ToDomain(dataModel);
        Track(domain, dataModel, TrackedEntryState.Unchanged);
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
            Track(domain, d, TrackedEntryState.Unchanged);
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

    // ──────────────────────────────────────────────────────────────
    // ITrackedRepository — used by UnitOfWork
    // ──────────────────────────────────────────────────────────────

    public IReadOnlyCollection<TrackedEntry> GetTrackedEntries() => _trackedEntries.AsReadOnly();

    public virtual async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (id, entry) in _identityMap.ToList())
        {
            switch (entry.State)
            {
                case TrackedEntryState.Added:
                    var newData = _mapper.ToData(entry.Domain);
                    _context.Set<TDataModel>().Add(newData);
                    break;

                case TrackedEntryState.Modified:
                    if (entry.Data is not null)
                    {
                        _mapper.ApplyChanges(entry.Domain, entry.Data);
                        _context.Set<TDataModel>().Update(entry.Data);
                    }
                    else
                    {
                        var updatedData = _mapper.ToData(entry.Domain);
                        _context.Set<TDataModel>().Update(updatedData);
                    }
                    break;

                case TrackedEntryState.Removed:
                    if (entry.Data is not null)
                    {
                        _context.Set<TDataModel>().Remove(entry.Data);
                    }
                    else
                    {
                        var toRemove = _mapper.ToData(entry.Domain);
                        _context.Set<TDataModel>().Attach(toRemove);
                        _context.Set<TDataModel>().Remove(toRemove);
                    }
                    break;
            }
        }

        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────────────────────
    // Internal tracking helpers
    // ──────────────────────────────────────────────────────────────

    private void Track(TDomainModel domain, TDataModel? data, TrackedEntryState state)
    {
        if (_identityMap.ContainsKey(domain.Id))
            return; // Already tracked

        _identityMap[domain.Id] = (domain, data, state);
        _trackedEntries.Add(new TrackedEntry(domain, state));
    }

    private void UpdateTrackedEntryState(TDomainModel domain, TrackedEntryState newState)
    {
        var entry = _trackedEntries.FirstOrDefault(e => e.DomainEntity.Id == domain.Id);
        if (entry is not null)
        {
            entry.State = newState;
        }
    }
}
