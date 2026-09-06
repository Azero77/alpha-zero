using AlphaZero.Modules.Identity.Application.Principals.Commands.AttachManagedPolicy;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Commands.AttachManagedPolicy;

public record AttachManagedPolicyRequest
{
    public Guid PrincipalId { get; init; }
    public Guid ManagedPolicyId { get; init; }
}

public class AttachManagedPolicySummary : Summary<AttachManagedPolicyEndpoint>
{
    public AttachManagedPolicySummary()
    {
        Summary = "Attaches a managed policy to a principal";
        Description = "Assigns an existing managed policy to an IAM principal.";
        Response(204, "Managed policy attached successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (PrincipalId or ManagedPolicyId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Principal or managed policy not found (Principal.NotFound, ManagedPolicy.NotFound)");
    }
}

public class AttachManagedPolicyEndpoint : Endpoint<AttachManagedPolicyRequest>
{
    private readonly IdentityModule _module;

    public AttachManagedPolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        // This is still valid as it manages the relationship via the application layer
        Post("/identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}");
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new AttachManagedPolicySummary());
    }

    public override async Task HandleAsync(AttachManagedPolicyRequest req, CancellationToken ct)
    {
        var command = new AttachManagedPolicyCommand(req.PrincipalId, req.ManagedPolicyId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
