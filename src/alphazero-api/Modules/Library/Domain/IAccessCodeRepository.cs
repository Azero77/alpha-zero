using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

namespace AlphaZero.Modules.Library.Domain;

public interface IAccessCodeRepository : IRepository<AccessCode>
{
    Task<bool> BatchExistsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<int> CountBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashForUpdateAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<List<AccessCode>> GetBatchForUpdateAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pessimistic mass-load of access codes whose expiry has passed and that are still in a
    /// non-terminal state (Minted/Distributed/Sold) — the expiry sweep's guarded path (issue #59).
    /// Orders by "Id" (documented lock order) and requires an active ambient EF transaction.
    /// </summary>
    Task<List<AccessCode>> GetDueForExpiryAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<ErrorOr<Success>> UpdateAsync(AccessCode code, CancellationToken cancellationToken = default);
}
