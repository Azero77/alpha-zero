using AlphaZero.Modules.Library.Application.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;

public record LibraryDto(Guid Id, string Name, string Address, string ContactNumber, List<string> AllowedResources);

public record GetLibraryQuery(Guid Id) : IRequest<ErrorOr<LibraryDto>>;

public sealed class GetLibraryQueryHandler(ILibraryQueryService libraryQueryService) : IRequestHandler<GetLibraryQuery, ErrorOr<LibraryDto>>
{
    public async Task<ErrorOr<LibraryDto>> Handle(GetLibraryQuery request, CancellationToken cancellationToken)
    {
        var library = await libraryQueryService.GetLibraryByIdAsync(request.Id, cancellationToken);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        return library;
    }
}
