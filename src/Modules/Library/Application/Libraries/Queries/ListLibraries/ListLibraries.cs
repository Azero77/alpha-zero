using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Libraries.Queries.ListLibraries;

public record ListLibrariesQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<LibraryDto>>>;

public sealed class ListLibrariesQueryHandler(ILibraryQueryService libraryQueryService) : IRequestHandler<ListLibrariesQuery, ErrorOr<PagedResult<LibraryDto>>>
{
    public async Task<ErrorOr<PagedResult<LibraryDto>>> Handle(ListLibrariesQuery request, CancellationToken cancellationToken)
    {
        return await libraryQueryService.ListLibrariesAsync(request.Page, request.PerPage, cancellationToken);
    }
}
