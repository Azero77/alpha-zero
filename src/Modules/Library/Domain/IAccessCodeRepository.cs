using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Library.Domain;

public interface IAccessCodeRepository : IRepository<AccessCode>
{
    Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default);
}
