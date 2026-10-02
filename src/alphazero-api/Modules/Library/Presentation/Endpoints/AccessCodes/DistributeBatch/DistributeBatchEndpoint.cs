using AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.DistributeBatch;

public record DistributeBatchRequest { public Guid BatchId { get; init; } }

public class DistributeBatchSummary : Summary<DistributeBatchEndpoint>
{
    public DistributeBatchSummary()
    {
        Summary = "Distributes a batch of access codes";
        Description = "Marks a batch of minted access codes as distributed to libraries for sale.";
        Response(204, "Batch distributed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (BatchId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:SellCodes permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Invalid code status (AccessCode.InvalidStatus)");
    }
}

public class DistributeBatchEndpoint(LibraryModule module) : Endpoint<DistributeBatchRequest>
{
    public override void Configure()
    {
        Post("/library/access-codes/batches/{BatchId}/distribute");
        // Distributing batches is an administrative/accountant task
        this.AccessControl("library:SellCodes", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library"));
        Summary(new DistributeBatchSummary());
    }

    public override async Task HandleAsync(DistributeBatchRequest req, CancellationToken ct)
    {
        var command = new DistributeBatchCommand(req.BatchId);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
