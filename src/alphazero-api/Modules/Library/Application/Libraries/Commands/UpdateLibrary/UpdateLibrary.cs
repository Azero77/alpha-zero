using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.Libraries.Commands.UpdateLibrary;

public record UpdateLibraryCommand(Guid Id, string Name, string Address, string ContactNumber) : ICommand<Success>;

public class UpdateLibraryCommandValidator : AbstractValidator<UpdateLibraryCommand>
{
    public UpdateLibraryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(512);
        RuleFor(x => x.ContactNumber).NotEmpty().MaximumLength(32);
    }
}

public sealed class UpdateLibraryCommandHandler(
    ILibraryRepository libraryRepository,
    ILogger<UpdateLibraryCommandHandler> logger) : IRequestHandler<UpdateLibraryCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateLibraryCommand request, CancellationToken cancellationToken)
    {
        var library = await libraryRepository.GetById(request.Id);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        library.Update(request.Name, request.Address, request.ContactNumber);
        libraryRepository.Update(library);
        
        logger.LogInformation("Library {LibraryId} updated.", request.Id);

        return Result.Success;
    }
}
