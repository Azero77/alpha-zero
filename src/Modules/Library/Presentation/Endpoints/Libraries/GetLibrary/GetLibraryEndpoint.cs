using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.GetLibrary;

public record GetLibraryRequest { public Guid Id { get; init; } }

public class GetLibraryEndpoint(LibraryModule module) : Endpoint<GetLibraryRequest, LibraryDto>
{
    public override void Configure()
    {
        Get("/library/libraries/{Id}");
        this.AccessControl("library:Audit", req => ResourceArn.ForLibrary(Guid.Empty, req.Id));
        Description(d => d.WithTags("Library Management"));
    }

    public override async Task HandleAsync(GetLibraryRequest req, CancellationToken ct)
    {
        var query = new GetLibraryQuery(req.Id);
        var result = await module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
