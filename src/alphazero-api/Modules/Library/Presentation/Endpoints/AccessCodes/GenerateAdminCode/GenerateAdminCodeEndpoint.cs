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

public class GenerateAdminCodeSummary : Summary<GenerateAdminCodeEndpoint>
{
    public GenerateAdminCodeSummary()
    {
        Summary = "Generates a single administrative access code";
        Description = "Generates a direct access code bypass without going through library batches.";
        Response<GenerateAdminCodeResponse>(200, "Admin access code generated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (TargetResourceArn or StrategyId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant not found or unauthenticated)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class GenerateAdminCodeEndpoint(LibraryModule module) : Endpoint<GenerateAdminCodeRequest, GenerateAdminCodeResponse>
{
    public override void Configure()
    {
        Post("/library/admin/access-codes/generate-single");
        // Restricted to Administrators/Accountants at the tenant level
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library Management"));
        Summary(new GenerateAdminCodeSummary());
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
