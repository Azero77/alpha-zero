using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using AlphaZero.Modules.Library.Domain;

namespace AlphaZero.Modules.Library.Infrastructure.Queries;

public class LibraryQueryService : ILibraryQueryService
{
    private readonly AppDbContext _context;

    public LibraryQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<LibraryDto>> ListLibrariesAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<AlphaZero.Modules.Library.Domain.Library>().AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(l => new LibraryDto(
                l.Id,
                l.Name,
                l.Address,
                l.ContactNumber,
                l.AllowedResources.Select(r => r.Value).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<LibraryDto>(items, totalCount, page, perPage);
    }
}
