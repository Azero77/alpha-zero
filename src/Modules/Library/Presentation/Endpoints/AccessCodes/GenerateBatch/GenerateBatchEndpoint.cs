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
        this.AccessControl("library:GenerateCodes", req => ResourceArn.ForLibrary(Guid.Empty, req.LibraryId));
        Description(d => d.WithTags("Library"));
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
