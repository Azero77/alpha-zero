using AlphaZero.Modules.Library.Application.AccessCodes.GenerateBatch;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateBatch;

public record GenerateBatchRequest
{
    public Guid LibraryId { get; init; }
    public int Quantity { get; init; }
    public string StrategyId { get; init; } = "enroll-course";
    public string TargetResourceArn { get; init; } = default!;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record GenerateBatchResponse(List<string> Codes);

public class GenerateBatchSummary : Summary<GenerateBatchEndpoint>
{
    public GenerateBatchSummary()
    {
        Summary = "Generates a batch of access codes for a library";
        Description = "Generates physical or digital access codes linked to a course/resource for distribution.";
        Response<GenerateBatchResponse>(200, "Access codes generated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Quantity <= 0 or > 1000, StrategyId or TargetResourceArn empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant not found or unauthenticated)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:GenerateCodes or library not authorized for resource)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Library not found (Library.NotFound)");
    }
}

public class GenerateBatchEndpoint : Endpoint<GenerateBatchRequest, GenerateBatchResponse>
{
    private readonly LibraryModule _module;

    public GenerateBatchEndpoint(LibraryModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/library/libraries/{LibraryId}/access-codes/generate");
        this.AccessControl("library:GenerateCodes", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.LibraryId));
        Description(d => d.WithTags("Library"));
        Summary(new GenerateBatchSummary());
    }

    public override async Task HandleAsync(GenerateBatchRequest req, CancellationToken ct)
    {
        var command = new GenerateBatchCommand(req.LibraryId, req.Quantity, req.StrategyId, req.TargetResourceArn, req.Metadata);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new GenerateBatchResponse(result.Value), ct);
    }
}
