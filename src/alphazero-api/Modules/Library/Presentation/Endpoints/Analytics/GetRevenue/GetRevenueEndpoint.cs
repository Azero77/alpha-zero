using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryRevenue;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Analytics.GetRevenue;

public record GetLibraryRevenueRequest
{
    public Guid? LibraryId { get; init; }
}

public class GetLibraryRevenueSummary : Summary<GetLibraryRevenueEndpoint>
{
    public GetLibraryRevenueSummary()
    {
        Summary = "Revenue per period (P4 analytics)";
        Description = "Returns revenue at face value per month (UTC) and per currency, derived exclusively from sale records. Tenant-wide when LibraryId is omitted; library-scoped when present.";
        Response<LibraryRevenueDto>(200, "Revenue retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId empty or invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class GetLibraryRevenueEndpoint(LibraryModule module) : Endpoint<GetLibraryRevenueRequest, LibraryRevenueDto>
{
    public override void Configure()
    {
        Get("/library/analytics/revenue");
        this.AccessControl("library:Audit", (req, tenantId) =>
            req.LibraryId is Guid libraryId && libraryId != Guid.Empty
                ? ResourceArn.ForLibrary(tenantId, libraryId)
                : ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Analytics"));
        Summary(new GetLibraryRevenueSummary());
    }

    public override async Task HandleAsync(GetLibraryRevenueRequest req, CancellationToken ct)
    {
        var result = await module.Send(new GetLibraryRevenueQuery(req.LibraryId), ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
