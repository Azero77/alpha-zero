using AlphaZero.Modules.Library.Application.Libraries.Commands.DeleteLibrary;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.DeleteLibrary;

public record DeleteLibraryRequest { public Guid Id { get; init; } }

public class DeleteLibrarySummary : Summary<DeleteLibraryEndpoint>
{
    public DeleteLibrarySummary()
    {
        Summary = "Deletes a partner library";
        Description = "Removes a partner library from the academy system.";
        Response(204, "Library deleted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Library not found (Library.NotFound)");
    }
}

public class DeleteLibraryEndpoint(LibraryModule module) : Endpoint<DeleteLibraryRequest>
{
    public override void Configure()
    {
        Delete("/library/libraries/{Id}");
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.Id));
        Description(d => d.WithTags("Library Management"));
        Summary(new DeleteLibrarySummary());
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
