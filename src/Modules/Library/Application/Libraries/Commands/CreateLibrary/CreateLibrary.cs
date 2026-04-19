using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.Libraries.Commands.CreateLibrary;

public record CreateLibraryCommand(string Name, string Address, string ContactNumber) : ICommand<Guid>;

public sealed class CreateLibraryCommandHandler(
    ILibraryRepository libraryRepository,
    ITenantProvider tenantProvider,
    ILogger<CreateLibraryCommandHandler> logger) : IRequestHandler<CreateLibraryCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateLibraryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var library = Domain.Library.Create(request.Name, request.Address, request.ContactNumber, tenantId.Value);

        libraryRepository.Add(library);
        logger.LogInformation("Library '{LibraryName}' created with ID {LibraryId} for Tenant {TenantId}.", 
            request.Name, library.Id, tenantId.Value);

        return library.Id;
    }
}
