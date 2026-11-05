using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryFunnel;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Analytics.GetFunnel;

public record GetLibraryFunnelRequest
{
    public Guid? LibraryId { get; init; }
}

public class GetLibraryFunnelSummary : Summary<GetLibraryFunnelEndpoint>
{
    public GetLibraryFunnelSummary()
    {
        Summary = "Inventory funnel (P4 analytics)";
        Description = "Returns the access-code lifecycle funnel (minted/distributed/sold/redeemed/voided/expired/outstanding/voided-after-sale). Tenant-wide when LibraryId is omitted; library-scoped when present. Sold counts derive from sale records.";
        Response<LibraryFunnelDto>(200, "Funnel retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId empty or invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class GetLibraryFunnelEndpoint(LibraryModule module) : Endpoint<GetLibraryFunnelRequest, LibraryFunnelDto>
{
    public override void Configure()
    {
        Get("/library/analytics/funnel");
        this.AccessControl("library:Audit", (req, tenantId) =>
            req.LibraryId is Guid libraryId && libraryId != Guid.Empty
                ? ResourceArn.ForLibrary(tenantId, libraryId)
                : ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Analytics"));
        Summary(new GetLibraryFunnelSummary());
    }

    public override async Task HandleAsync(GetLibraryFunnelRequest req, CancellationToken ct)
    {
        var result = await module.Send(new GetLibraryFunnelQuery(req.LibraryId), ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
