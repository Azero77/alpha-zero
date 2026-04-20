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

public class AuthorizeResourceEndpoint(LibraryModule module) : Endpoint<AuthorizeResourceRequest>
{
    public override void Configure()
    {
        Post("/library/libraries/{Id}/resources");
        this.AccessControl("library:AttachCourses", req => ResourceArn.ForLibrary(Guid.Empty, req.Id));
        Description(d => d.WithTags("Library Management"));
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
