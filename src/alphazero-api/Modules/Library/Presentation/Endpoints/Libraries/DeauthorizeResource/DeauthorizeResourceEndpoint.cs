using AlphaZero.Modules.Library.Application.Libraries.Commands.DeauthorizeResource;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.DeauthorizeResource;

public record DeauthorizeResourceRequest
{
    public Guid Id { get; init; }
    public string ResourceArn { get; init; } = default!;
}

public class DeauthorizeResourceSummary : Summary<DeauthorizeResourceEndpoint>
{
    public DeauthorizeResourceSummary()
    {
        Summary = "Deauthorizes a resource from a library";
        Description = "Revokes a partner library's permission to sell access codes for the specified course/resource.";
        Response(204, "Resource deauthorized from library successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId or ResourceArn empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:AttachCourses permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Library not found or resource not authorized (Library.NotFound, Library.ResourceNotAuthorized)");
    }
}

public class DeauthorizeResourceEndpoint(LibraryModule module) : Endpoint<DeauthorizeResourceRequest>
{
    public override void Configure()
    {
        Delete("/library/libraries/{Id}/resources");
        this.AccessControl("library:AttachCourses", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.Id));
        Description(d => d.WithTags("Library Management"));
        Summary(new DeauthorizeResourceSummary());
    }

    public override async Task HandleAsync(DeauthorizeResourceRequest req, CancellationToken ct)
    {
        var command = new DeauthorizeResourceCommand(req.Id, req.ResourceArn);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
