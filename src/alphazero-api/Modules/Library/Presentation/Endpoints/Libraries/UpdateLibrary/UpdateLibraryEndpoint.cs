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

public class UpdateLibrarySummary : Summary<UpdateLibraryEndpoint>
{
    public UpdateLibrarySummary()
    {
        Summary = "Updates partner library information";
        Description = "Updates the name, address, or contact number of an existing partner library.";
        Response(204, "Library updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Id, Name, Address, ContactNumber empty or too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Library not found (Library.NotFound)");
    }
}

public class UpdateLibraryEndpoint(LibraryModule module) : Endpoint<UpdateLibraryRequest>
{
    public override void Configure()
    {
        Patch("/library/libraries/{Id}");
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.Id));
        Description(d => d.WithTags("Library Management"));
        Summary(new UpdateLibrarySummary());
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
