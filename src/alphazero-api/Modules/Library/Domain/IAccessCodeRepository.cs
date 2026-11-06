using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

namespace AlphaZero.Modules.Library.Domain;

public interface IAccessCodeRepository : IRepository<AccessCode>
{
    Task<bool> BatchExistsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<int> CountBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashForUpdateAsync(string codeHash, CancellationToken cancellationToken = default);
    /// <summary>
    /// Pessimistic row-level lock on an entire batch. Acquires Postgres FOR UPDATE locks ordered
    /// deterministically by Id to prevent deadlocks. The <paramref name="tenantId"/> filter is
    /// applied INSIDE the locking statement so rows of another tenant are never locked, read,
    /// or enumerated (I-TENANT: lock ordering must not reach across tenants).
    /// </summary>
    Task<List<AccessCode>> GetBatchForUpdateAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pessimistic mass-load of access codes whose expiry has passed and that are still in a
    /// non-terminal state (Minted/Distributed/Sold) — the expiry sweep's guarded path (issue #59).
    /// Orders by "Id" (documented lock order) and requires an active ambient EF transaction.
    /// </summary>
    Task<List<AccessCode>> GetDueForExpiryAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<ErrorOr<Success>> UpdateAsync(AccessCode code, CancellationToken cancellationToken = default);

    Task<int> UpdateBatch(Guid tenantId, Guid batchId, AccessCodeStatus? prevStatus, AccessCodeStatus newStatus,
        CancellationToken cancellationToken = default);
}
