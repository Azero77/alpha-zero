using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;

public record LibraryDto(Guid Id, string Name, string Address, string ContactNumber, List<string> AllowedResources);

public record GetLibraryQuery(Guid Id) : IRequest<ErrorOr<LibraryDto>>;

public sealed class GetLibraryQueryHandler(ILibraryRepository libraryRepository) : IRequestHandler<GetLibraryQuery, ErrorOr<LibraryDto>>
{
    public async Task<ErrorOr<LibraryDto>> Handle(GetLibraryQuery request, CancellationToken cancellationToken)
    {
        var library = await libraryRepository.GetById(request.Id);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        return new LibraryDto(
            library.Id, 
            library.Name, 
            library.Address, 
            library.ContactNumber, 
            library.AllowedResources.Select(r => r.Value).ToList());
    }
}
