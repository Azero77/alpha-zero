using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Library.Application.Queries;

public interface ILibraryQueryService
{
    Task<PagedResult<LibraryDto>> ListLibrariesAsync(int page, int perPage, CancellationToken cancellationToken = default);
}
