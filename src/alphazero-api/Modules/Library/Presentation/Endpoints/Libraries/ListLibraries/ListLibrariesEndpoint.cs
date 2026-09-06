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

public class ListLibrariesSummary : Summary<ListLibrariesEndpoint>
{
    public ListLibrariesSummary()
    {
        Summary = "Lists partner libraries";
        Description = "Retrieves a paginated list of registered partner libraries.";
        Response<PagedResult<LibraryDto>>(200, "Libraries retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class ListLibrariesEndpoint(LibraryModule module) : Endpoint<ListLibrariesRequest, PagedResult<LibraryDto>>
{
    public override void Configure()
    {
        Get("/library/libraries");
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library Management"));
        Summary(new ListLibrariesSummary());
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
