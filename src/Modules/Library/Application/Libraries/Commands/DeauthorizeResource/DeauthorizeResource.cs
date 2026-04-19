using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.Libraries.Commands.DeauthorizeResource;

public record DeauthorizeResourceCommand(Guid LibraryId, string ResourceArn) : ICommand<Success>;

public sealed class DeauthorizeResourceCommandHandler(
    ILibraryRepository libraryRepository,
    ILogger<DeauthorizeResourceCommandHandler> logger) : IRequestHandler<DeauthorizeResourceCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeauthorizeResourceCommand request, CancellationToken cancellationToken)
    {
        var library = await libraryRepository.GetById(request.LibraryId);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        var arn = ResourceArn.Create(request.ResourceArn);
        if (arn.IsError) return arn.Errors;

        var result = library.DeauthorizeResource(arn.Value);
        if (result.IsError) return result.Errors;

        libraryRepository.Update(library);
        logger.LogInformation("Resource {ResourceArn} deauthorized from Library {LibraryId}.", request.ResourceArn, request.LibraryId);

        return Result.Success;
    }
}
