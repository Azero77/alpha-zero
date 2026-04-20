using AlphaZero.Modules.Library.Application.AccessCodes.GenerateAdminCode;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.GenerateAdminCode;

public record GenerateAdminCodeRequest
{
    public string TargetResourceArn { get; init; } = default!;
    public Dictionary<string, object>? Metadata { get; init; }
}

public record GenerateAdminCodeResponse(string Code);

public class GenerateAdminCodeEndpoint(LibraryModule module) : Endpoint<GenerateAdminCodeRequest, GenerateAdminCodeResponse>
{
    public override void Configure()
    {
        Post("/library/admin/access-codes/generate-single");
        // Restricted to Administrators/Accountants at the tenant level
        this.AccessControl("library:Audit", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Library Management"));
    }

    public override async Task HandleAsync(GenerateAdminCodeRequest req, CancellationToken ct)
    {
        var command = new GenerateAdminCodeCommand(req.TargetResourceArn, Metadata: req.Metadata);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new GenerateAdminCodeResponse(result.Value), ct);
    }
}
