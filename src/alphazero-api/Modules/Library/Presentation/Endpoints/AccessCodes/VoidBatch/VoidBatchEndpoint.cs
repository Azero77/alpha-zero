using AlphaZero.Modules.Library.Application.AccessCodes.VoidBatch;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.VoidBatch;

public record VoidBatchRequest
{
    public Guid BatchId { get; init; }
    public string Reason { get; init; } = default!;
}

public record VoidBatchResponse
{
    public Guid BatchId { get; init; }
    public int TotalCodes { get; init; }
    public int VoidedCount { get; init; }
    public int SkippedCount { get; init; }
    public List<Guid> VoidedCodeIds { get; init; } = new();
}

public class VoidBatchSummary : Summary<VoidBatchEndpoint>
{
    public VoidBatchSummary()
    {
        Summary = "Recalls and voids an entire batch of access codes";
        Description = "Voids all voidable access codes in a batch with a mandatory reason. Terminal codes are skipped idempotently.";
        Response<VoidBatchResponse>(200, "Batch recalled successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Empty reason or size cap exceeded)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:SellCodes permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Batch not found (Batch.NotFound)");
    }
}

public class VoidBatchEndpoint(LibraryModule module) : Endpoint<VoidBatchRequest, VoidBatchResponse>
{
    public override void Configure()
    {
        Post("/library/access-codes/batches/{BatchId}/void");
        this.AccessControl("library:SellCodes", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library"));
        Summary(new VoidBatchSummary());
    }

    public override async Task HandleAsync(VoidBatchRequest req, CancellationToken ct)
    {
        var command = new VoidBatchCommand(req.BatchId, req.Reason);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new VoidBatchResponse
        {
            BatchId = result.Value.BatchId,
            TotalCodes = result.Value.TotalCodes,
            VoidedCount = result.Value.VoidedCount,
            SkippedCount = result.Value.SkippedCount,
            VoidedCodeIds = result.Value.VoidedCodeIds
        }, ct);
    }
}
