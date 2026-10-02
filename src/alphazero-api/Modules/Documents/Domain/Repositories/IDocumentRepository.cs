using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Documents.Domain.Repositories;

public interface IDocumentRepository : IRepository<Document>
{
    Task<List<Document>> ListAsync(string? fileType = null, CancellationToken cancellationToken = default);
}
