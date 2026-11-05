using AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.Analytics.GetProvisioningQueue;

public record GetProvisioningQueueRequest
{
    public Guid? LibraryId { get; init; }
}

public class GetProvisioningQueueSummary : Summary<GetProvisioningQueueEndpoint>
{
    public GetProvisioningQueueSummary()
    {
        Summary = "Failed-provisioning queue (P4 analytics)";
        Description = "Returns activated access codes whose provisioning in the Courses module failed or is parked (reconciled via the cross-module read contract). Paying students are never silently lost.";
        Response<List<FailedProvisioningDto>>(200, "Provisioning queue retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId empty or invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class GetProvisioningQueueEndpoint(LibraryModule module) : Endpoint<GetProvisioningQueueRequest, List<FailedProvisioningDto>>
{
    public override void Configure()
    {
        Get("/library/analytics/provisioning-queue");
        this.AccessControl("library:Audit", (req, tenantId) =>
            req.LibraryId is Guid libraryId && libraryId != Guid.Empty
                ? ResourceArn.ForLibrary(tenantId, libraryId)
                : ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Analytics"));
        Summary(new GetProvisioningQueueSummary());
    }

    public override async Task HandleAsync(GetProvisioningQueueRequest req, CancellationToken ct)
    {
        var result = await module.Send(new GetProvisioningQueueQuery(req.LibraryId), ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
