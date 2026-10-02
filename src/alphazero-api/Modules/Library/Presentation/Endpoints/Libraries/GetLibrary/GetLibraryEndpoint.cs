using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Libraries.GetLibrary;

public record GetLibraryRequest { public Guid Id { get; init; } }

public class GetLibrarySummary : Summary<GetLibraryEndpoint>
{
    public GetLibrarySummary()
    {
        Summary = "Gets a library by ID";
        Description = "Retrieves details of a registered partner library.";
        Response<LibraryDto>(200, "Library retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Library not found (Library.NotFound)");
    }
}

public class GetLibraryEndpoint(LibraryModule module) : Endpoint<GetLibraryRequest, LibraryDto>
{
    public override void Configure()
    {
        Get("/library/libraries/{Id}");
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.Id));
        Description(d => d.WithTags("Library Management"));
        Summary(new GetLibrarySummary());
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
