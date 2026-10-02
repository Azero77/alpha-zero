using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Documents.Infrastructure.Repositories;

public class DocumentRepository : BaseRepository<AppDbContext, Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Document>> ListAsync(string? fileType = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Documents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(fileType))
        {
            var normalized = fileType.ToLowerInvariant().TrimStart('.');
            query = query.Where(d => d.FileType == normalized);
        }
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            query.OrderByDescending(d => d.CreatedOn), cancellationToken);
    }
}
