using AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.DistributeBatch;

public record DistributeBatchRequest { public Guid BatchId { get; init; } }

public class DistributeBatchEndpoint(LibraryModule module) : Endpoint<DistributeBatchRequest>
{
    public override void Configure()
    {
        Post("/library/access-codes/batches/{BatchId}/distribute");
        // Distributing batches is an administrative/accountant task
        this.AccessControl("library:SellCodes", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Library"));
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
