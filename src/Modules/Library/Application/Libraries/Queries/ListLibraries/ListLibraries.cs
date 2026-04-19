using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;
using System.Linq;

namespace AlphaZero.Modules.Library.Application.Libraries.Queries.ListLibraries;

public record ListLibrariesQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<LibraryDto>>>;

public sealed class ListLibrariesQueryHandler(ILibraryRepository libraryRepository) : IRequestHandler<ListLibrariesQuery, ErrorOr<PagedResult<LibraryDto>>>
{
    public async Task<ErrorOr<PagedResult<LibraryDto>>> Handle(ListLibrariesQuery request, CancellationToken cancellationToken)
    {
        var result = await libraryRepository.GetPagedAsync(request.Page, request.PerPage, cancellationToken);

        var dtos = result.Items.Select(library => new LibraryDto(
            library.Id,
            library.Name,
            library.Address,
            library.ContactNumber,
            library.AllowedResources.Select(r => r.Value).ToList())).ToList();

        return new PagedResult<LibraryDto>(dtos, result.TotalCount, result.CurrentPage, result.PageSize);
    }
}
