using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Library.Domain;

public interface IAccessCodeRepository : IRepository<AccessCode>
{
    Task<int> MarkBatchAsDistributedAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default);
}
