using AlphaZero.Modules.Library.Application.Analytics.Queries.GetFraudFlags;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Analytics.GetFraudQueue;

public record GetFraudQueueRequest
{
    public Guid? LibraryId { get; init; }
}

public class GetFraudQueueSummary : Summary<GetFraudQueueEndpoint>
{
    public GetFraudQueueSummary()
    {
        Summary = "Fraud-flag queue (P4 analytics)";
        Description = "Returns fraud flags computed over the append-only audit stream: unsold redemptions, void-after-redeem, and redemption velocity anomalies. Each row links to its audit entries.";
        Response<List<FraudFlagDto>>(200, "Fraud queue retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId empty or invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class GetFraudQueueEndpoint(LibraryModule module) : Endpoint<GetFraudQueueRequest, List<FraudFlagDto>>
{
    public override void Configure()
    {
        Get("/library/analytics/fraud-queue");
        this.AccessControl("library:Audit", (req, tenantId) =>
            req.LibraryId is Guid libraryId && libraryId != Guid.Empty
                ? ResourceArn.ForLibrary(tenantId, libraryId)
                : ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Analytics"));
        Summary(new GetFraudQueueSummary());
    }

    public override async Task HandleAsync(GetFraudQueueRequest req, CancellationToken ct)
    {
        var result = await module.Send(new GetFraudQueueQuery(req.LibraryId), ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
