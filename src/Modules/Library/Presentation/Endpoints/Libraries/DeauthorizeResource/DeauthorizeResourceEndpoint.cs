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

public class DeauthorizeResourceEndpoint(LibraryModule module) : Endpoint<DeauthorizeResourceRequest>
{
    public override void Configure()
    {
        Delete("/library/libraries/{Id}/resources");
        this.AccessControl("library:AttachCourses", req => ResourceArn.ForLibrary(Guid.Empty, req.Id));
        Description(d => d.WithTags("Library Management"));
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
