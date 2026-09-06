using AlphaZero.Modules.Identity.Application.Principals.Commands.DetachManagedPolicy;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Commands.DetachManagedPolicy;

public record DetachManagedPolicyRequest
{
    public Guid PrincipalId { get; init; }
    public Guid ManagedPolicyId { get; init; }
}

public class DetachManagedPolicySummary : Summary<DetachManagedPolicyEndpoint>
{
    public DetachManagedPolicySummary()
    {
        Summary = "Detaches a managed policy from a principal";
        Description = "Removes a managed policy association from an IAM principal.";
        Response(204, "Managed policy detached successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (PrincipalId or ManagedPolicyId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Principal not found (Principal.NotFound)");
    }
}

public class DetachManagedPolicyEndpoint : Endpoint<DetachManagedPolicyRequest>
{
    private readonly IdentityModule _module;

    public DetachManagedPolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}");
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new DetachManagedPolicySummary());
    }

    public override async Task HandleAsync(DetachManagedPolicyRequest req, CancellationToken ct)
    {
        var command = new DetachManagedPolicyCommand(req.PrincipalId, req.ManagedPolicyId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
