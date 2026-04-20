using AlphaZero.Modules.Library.Application.Libraries.Commands.DeleteLibrary;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.DeleteLibrary;

public record DeleteLibraryRequest { public Guid Id { get; init; } }

public class DeleteLibraryEndpoint(LibraryModule module) : Endpoint<DeleteLibraryRequest>
{
    public override void Configure()
    {
        Delete("/library/libraries/{Id}");
        this.AccessControl("library:Audit", req => ResourceArn.ForLibrary(Guid.Empty, req.Id));
        Description(d => d.WithTags("Library Management"));
    }

    public override async Task HandleAsync(DeleteLibraryRequest req, CancellationToken ct)
    {
        var command = new DeleteLibraryCommand(req.Id);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
