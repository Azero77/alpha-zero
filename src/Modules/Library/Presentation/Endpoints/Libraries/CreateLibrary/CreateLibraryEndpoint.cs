using AlphaZero.Modules.Library.Application.Libraries.Commands.CreateLibrary;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.CreateLibrary;

public record CreateLibraryRequest
{
    public string Name { get; init; } = default!;
    public string Address { get; init; } = default!;
    public string ContactNumber { get; init; } = default!;
}

public record CreateLibraryResponse(Guid Id);

public class CreateLibraryEndpoint(LibraryModule module) : Endpoint<CreateLibraryRequest, CreateLibraryResponse>
{
    public override void Configure()
    {
        Post("/library/libraries");
        this.AccessControl("library:Audit", _ => ResourceArn.ForTenant(Guid.Empty)); // Only accountants/admins can create libraries
        Description(d => d.WithTags("Library Management"));
    }

    public override async Task HandleAsync(CreateLibraryRequest req, CancellationToken ct)
    {
        var command = new CreateLibraryCommand(req.Name, req.Address, req.ContactNumber);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/library/libraries/{result.Value}", new CreateLibraryResponse(result.Value), cancellation: ct);
    }
}
