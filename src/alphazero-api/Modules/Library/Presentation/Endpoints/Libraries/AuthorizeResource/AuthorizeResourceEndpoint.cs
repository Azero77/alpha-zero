using AlphaZero.Modules.Library.Application.Libraries.Commands.AuthorizeResource;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.AuthorizeResource;

public record AuthorizeResourceRequest
{
    public Guid Id { get; init; }
    public string ResourceArn { get; init; } = default!;
}

public class AuthorizeResourceSummary : Summary<AuthorizeResourceEndpoint>
{
    public AuthorizeResourceSummary()
    {
        Summary = "Authorizes a resource for a library";
        Description = "Enables a partner library to sell access codes for the specified course/resource.";
        Response(204, "Resource authorized for library successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId or ResourceArn empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:AttachCourses permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Library not found (Library.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Resource already authorized (Library.ResourceAlreadyAuthorized)");
    }
}

public class AuthorizeResourceEndpoint(LibraryModule module) : Endpoint<AuthorizeResourceRequest>
{
    public override void Configure()
    {
        Post("/library/libraries/{Id}/resources");
        this.AccessControl("library:AttachCourses", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.Id));
        Description(d => d.WithTags("Library Management"));
        Summary(new AuthorizeResourceSummary());
    }

    public override async Task HandleAsync(AuthorizeResourceRequest req, CancellationToken ct)
    {
        var command = new AuthorizeResourceCommand(req.Id, req.ResourceArn);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
