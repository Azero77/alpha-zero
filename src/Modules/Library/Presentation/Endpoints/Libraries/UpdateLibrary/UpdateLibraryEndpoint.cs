using AlphaZero.Modules.Library.Application.Libraries.Commands.UpdateLibrary;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.UpdateLibrary;

public record UpdateLibraryRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Address { get; init; } = default!;
    public string ContactNumber { get; init; } = default!;
}

public class UpdateLibraryEndpoint(LibraryModule module) : Endpoint<UpdateLibraryRequest>
{
    public override void Configure()
    {
        Patch("/library/libraries/{Id}");
        this.AccessControl("library:Audit", req => ResourceArn.ForLibrary(Guid.Empty, req.Id));
        Description(d => d.WithTags("Library Management"));
    }

    public override async Task HandleAsync(UpdateLibraryRequest req, CancellationToken ct)
    {
        var command = new UpdateLibraryCommand(req.Id, req.Name, req.Address, req.ContactNumber);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
