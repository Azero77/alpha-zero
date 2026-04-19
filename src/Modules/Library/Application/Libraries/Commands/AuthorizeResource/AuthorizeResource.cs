using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.Libraries.Commands.AuthorizeResource;

public record AuthorizeResourceCommand(Guid LibraryId, string ResourceArn) : ICommand<Success>;

public sealed class AuthorizeResourceCommandHandler(
    ILibraryRepository libraryRepository,
    ILogger<AuthorizeResourceCommandHandler> logger) : IRequestHandler<AuthorizeResourceCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(AuthorizeResourceCommand request, CancellationToken cancellationToken)
    {
        var library = await libraryRepository.GetById(request.LibraryId);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        var arn = ResourceArn.Create(request.ResourceArn);
        if (arn.IsError) return arn.Errors;

        var result = library.AuthorizeResource(arn.Value);
        if (result.IsError) return result.Errors;

        libraryRepository.Update(library);
        logger.LogInformation("Resource {ResourceArn} authorized for Library {LibraryId}.", request.ResourceArn, request.LibraryId);

        return Result.Success;
    }
}
