using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

namespace AlphaZero.Modules.Library.Domain;

public interface IAccessCodeRepository : IRepository<AccessCode>
{
    Task<bool> BatchExistsAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashForUpdateAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<List<AccessCode>> GetBatchForUpdateAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ErrorOr<Success>> UpdateAsync(AccessCode code, CancellationToken cancellationToken = default);
}
