using AlphaZero.Modules.Identity.Application.Principals.Commands.DetachInlinePolicy;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Commands.DetachInlinePolicy;

public record DetachInlinePolicyRequest
{
    public Guid PrincipalId { get; init; }
    public Guid PolicyId { get; init; }
}

public class DetachInlinePolicySummary : Summary<DetachInlinePolicyEndpoint>
{
    public DetachInlinePolicySummary()
    {
        Summary = "Detaches an inline policy from a principal";
        Description = "Removes an inline policy from an IAM principal.";
        Response(204, "Inline policy detached successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (PrincipalId or PolicyId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Principal not found (Principal.NotFound)");
    }
}

public class DetachInlinePolicyEndpoint : Endpoint<DetachInlinePolicyRequest>
{
    private readonly IdentityModule _module;

    public DetachInlinePolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/identity/principals/{PrincipalId}/policies/inline/{PolicyId}");
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new DetachInlinePolicySummary());
    }

    public override async Task HandleAsync(DetachInlinePolicyRequest req, CancellationToken ct)
    {
        var command = new DetachInlinePolicyCommand(req.PrincipalId, req.PolicyId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
