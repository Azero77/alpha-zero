using AlphaZero.Modules.Identity.Application.Principals.Commands.AttachInlinePolicy;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Commands.AttachInlinePolicy;

public record AttachInlinePolicyRequest
{
    public Guid PrincipalId { get; init; }
    public string PolicyName { get; init; } = default!;
    public List<PolicyStatement> Statements { get; init; } = new();
}

public class AttachInlinePolicySummary : Summary<AttachInlinePolicyEndpoint>
{
    public AttachInlinePolicySummary()
    {
        Summary = "Attaches an inline policy to a principal";
        Description = "Appends a custom inline policy to an IAM principal.";
        Response(204, "Inline policy attached successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (PrincipalId empty, PolicyName empty/too long, Statements empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant not found or invalid token)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Principal not found (Principal.NotFound)");
    }
}

public class AttachInlinePolicyEndpoint : Endpoint<AttachInlinePolicyRequest>
{
    private readonly IdentityModule _module;

    public AttachInlinePolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/principals/{PrincipalId}/policies/inline");
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new AttachInlinePolicySummary());
    }

    public override async Task HandleAsync(AttachInlinePolicyRequest req, CancellationToken ct)
    {
        var command = new AttachInlinePolicyCommand(req.PrincipalId, req.PolicyName, req.Statements);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
