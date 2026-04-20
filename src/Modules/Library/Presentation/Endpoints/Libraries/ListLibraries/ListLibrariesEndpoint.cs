using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.Libraries.Queries.ListLibraries;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.ListLibraries;

public record ListLibrariesRequest
{
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListLibrariesEndpoint(LibraryModule module) : Endpoint<ListLibrariesRequest, PagedResult<LibraryDto>>
{
    public override void Configure()
    {
        Get("/library/libraries");
        this.AccessControl("library:Audit", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Library Management"));
    }

    public override async Task HandleAsync(ListLibrariesRequest req, CancellationToken ct)
    {
        var query = new ListLibrariesQuery(req.Page, req.PerPage);
        var result = await module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
