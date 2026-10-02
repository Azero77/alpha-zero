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

public class CreateLibrarySummary : Summary<CreateLibraryEndpoint>
{
    public CreateLibrarySummary()
    {
        Summary = "Creates a new physical partner library";
        Description = "Registers a new partner library authorized to sell and distribute physical access codes.";
        Response<CreateLibraryResponse>(201, "Library created successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Name, Address, ContactNumber empty or too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant not found or unauthenticated)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class CreateLibraryEndpoint(LibraryModule module) : Endpoint<CreateLibraryRequest, CreateLibraryResponse>
{
    public override void Configure()
    {
        Post("/library/libraries");
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForTenant(tenantId)); // Only accountants/admins can create libraries
        Description(d => d.WithTags("Library Management"));
        Summary(new CreateLibrarySummary());
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

        await Send.CreatedAtAsync($"/library/libraries/{result.Value}", responseBody: new CreateLibraryResponse(result.Value), cancellation: ct);
    }
}
