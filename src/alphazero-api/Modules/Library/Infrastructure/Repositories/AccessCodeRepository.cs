using System.Linq.Expressions;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Models;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Application;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class AccessCodeRepository : IAccessCodeRepository
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private bool _ignoreQueryFilters;

    public AccessCodeRepository(AppDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public void Add(AccessCode entity)
    {
        var dataModel = MapToDataModel(entity);
        _context.AccessCodes.Add(dataModel);
        _unitOfWork.TrackEntity(entity);
    }

    public void Remove(AccessCode entity)
    {
        var dataModel = MapToDataModel(entity);
        _context.AccessCodes.Remove(dataModel);
    }

    public void Update(AccessCode entity)
    {
        _unitOfWork.TrackEntity(entity);
        var dataModel = MapToDataModel(entity);
        _context.AccessCodes.Update(dataModel);
    }

    public async Task<ErrorOr<Success>> UpdateAsync(AccessCode code, CancellationToken cancellationToken = default)
    {
        _unitOfWork.TrackEntity(code);

        // Tripwire: conditional WHERE status = expected
        if (code.ExpectedPreviousStatus.HasValue)
        {
            var expectedStatusStr = code.ExpectedPreviousStatus.Value.ToString();
            var newStatusStr = code.Status.ToString();

            var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Library"."AccessCodes"
                SET "Status" = {newStatusStr},
                    "RedeemedAt" = {code.RedeemedAt},
                    "RedeemedByUserId" = {code.RedeemedByUserId},
                    "VoidedReason" = {code.VoidedReason}
                WHERE "Id" = {code.Id} AND "Status" = {expectedStatusStr}
                """, cancellationToken);

            if (rowsAffected == 0)
            {
                return Error.Conflict(
                    "AccessCode.InvalidStatus",
                    $"Tripwire fired: expected status '{expectedStatusStr}' for code '{code.Id}', but row in database was modified concurrently.");
            }
        }
        else
        {
            var dataModel = MapToDataModel(code);
            _context.AccessCodes.Update(dataModel);
        }

        return Result.Success;
    }

    public async Task<AccessCode?> GetById(Guid id, CancellationToken token = default)
    {
        IQueryable<AccessCodeDataModel> query = _context.AccessCodes;
        if (_ignoreQueryFilters) query = query.IgnoreQueryFilters();

        var dataModel = await query.FirstOrDefaultAsync(x => x.Id == id, token);
        return dataModel is null ? null : MapToDomain(dataModel);
    }

    public async Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        var dataModel = await _context.AccessCodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);

        return dataModel is null ? null : MapToDomain(dataModel);
    }

    /// <summary>
    /// Pessimistic row-level lock (#54): acquires a Postgres FOR UPDATE lock on the access code row.
    /// Requires an active ambient EF transaction (managed by TransactionalBehavior).
    /// </summary>
    public async Task<AccessCode?> GetByHashForUpdateAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "GetByHashForUpdateAsync requires an active transaction; FOR UPDATE lock is released at statement end otherwise.");
        }

        var dataModel = await _context.AccessCodes
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Library"."AccessCodes"
                WHERE "CodeHash" = {codeHash}
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cancellationToken);

        return dataModel is null ? null : MapToDomain(dataModel);
    }

    /// <summary>
    /// Pessimistic row-level lock on an entire batch (#54).
    /// Acquires Postgres FOR UPDATE locks ordered deterministically by Id to prevent deadlocks.
    /// </summary>
    public async Task<List<AccessCode>> GetBatchForUpdateAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "GetBatchForUpdateAsync requires an active transaction; FOR UPDATE lock is released at statement end otherwise.");
        }

        var dataModels = await _context.AccessCodes
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Library"."AccessCodes"
                WHERE "BatchId" = {batchId}
                ORDER BY "Id"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        return dataModels.Select(MapToDomain).ToList();
    }

    public async Task<bool> BatchExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.AccessCodes
            .AnyAsync(x => x.BatchId == batchId, cancellationToken);
    }

    public async Task<int> CountBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.AccessCodes
            .CountAsync(x => x.BatchId == batchId, cancellationToken);
    }

    public void IgnoreQueryFilter()
    {
        _ignoreQueryFilters = true;
    }

    public async Task<AccessCode?> GetFirst(Expression<Func<AccessCode, bool>> filter, CancellationToken token = default)
    {
        var all = await GetAll(token);
        return all.AsQueryable().FirstOrDefault(filter);
    }

    public async Task<IReadOnlyCollection<AccessCode>> Get(Expression<Func<AccessCode, bool>> filter, CancellationToken token = default)
    {
        var all = await GetAll(token);
        return all.AsQueryable().Where(filter).ToList();
    }

    public async Task<IReadOnlyCollection<AccessCode>> GetAll(CancellationToken token = default)
    {
        IQueryable<AccessCodeDataModel> query = _context.AccessCodes;
        if (_ignoreQueryFilters) query = query.IgnoreQueryFilters();

        var list = await query.AsNoTracking().ToListAsync(token);
        return list.Select(MapToDomain).ToList();
    }

    public async Task<bool> Any(Expression<Func<AccessCode, bool>> filter, CancellationToken token = default)
    {
        var all = await GetAll(token);
        return all.AsQueryable().Any(filter);
    }

    public async Task<int> Count(Expression<Func<AccessCode, bool>>? filter = null, CancellationToken token = default)
    {
        var all = await GetAll(token);
        return filter is null ? all.Count : all.AsQueryable().Count(filter);
    }

    private static AccessCode MapToDomain(AccessCodeDataModel dataModel)
    {
        return AccessCode.Reconstitute(
            dataModel.Id,
            dataModel.CodeHash,
            dataModel.TenantId,
            dataModel.LibraryId,
            dataModel.StrategyId,
            dataModel.TargetResourceArn,
            dataModel.Metadata,
            dataModel.Status,
            dataModel.BatchId,
            dataModel.CreatedAt,
            dataModel.RedeemedAt,
            dataModel.RedeemedByUserId,
            dataModel.VoidedReason,
            dataModel.ExpiresAt);
    }

    private static AccessCodeDataModel MapToDataModel(AccessCode aggregate)
    {
        return new AccessCodeDataModel
        {
            Id = aggregate.Id,
            CodeHash = aggregate.CodeHash,
            TenantId = aggregate.TenantId,
            LibraryId = aggregate.LibraryId,
            StrategyId = aggregate.StrategyId,
            TargetResourceArn = aggregate.TargetResourceArn,
            Metadata = aggregate.Metadata,
            Status = aggregate.Status,
            BatchId = aggregate.BatchId,
            CreatedAt = aggregate.CreatedAt,
            RedeemedAt = aggregate.RedeemedAt,
            RedeemedByUserId = aggregate.RedeemedByUserId,
            VoidedReason = aggregate.VoidedReason,
            ExpiresAt = aggregate.ExpiresAt
        };
    }
}
