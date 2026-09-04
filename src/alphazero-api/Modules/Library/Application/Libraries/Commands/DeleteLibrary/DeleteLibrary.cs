using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.Libraries.Commands.DeleteLibrary;

public record DeleteLibraryCommand(Guid Id) : ICommand<Success>;

public sealed class DeleteLibraryCommandHandler(
    ILibraryRepository libraryRepository,
    ILogger<DeleteLibraryCommandHandler> logger) : IRequestHandler<DeleteLibraryCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteLibraryCommand request, CancellationToken cancellationToken)
    {
        var library = await libraryRepository.GetById(request.Id);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        libraryRepository.Remove(library);
        logger.LogInformation("Library {LibraryId} deleted.", request.Id);

        return Result.Success;
    }
}
