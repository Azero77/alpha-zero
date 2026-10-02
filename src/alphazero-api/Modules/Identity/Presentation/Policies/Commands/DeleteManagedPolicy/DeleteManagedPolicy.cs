using AlphaZero.Modules.Identity.Application.Policies.Commands.DeleteManagedPolicy;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Policies.Commands.DeleteManagedPolicy;

public record DeleteManagedPolicyRequest { public Guid PolicyId { get; init; } }

public class DeleteManagedPolicySummary : Summary<DeleteManagedPolicyEndpoint>
{
    public DeleteManagedPolicySummary()
    {
        Summary = "Deletes a managed policy";
        Description = "Permanently deletes a managed IAM policy.";
        Response(204, "Managed policy deleted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (PolicyId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePolicies permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Managed policy not found (ManagedPolicy.NotFound)");
    }
}

public class DeleteManagedPolicyEndpoint : Endpoint<DeleteManagedPolicyRequest>
{
    private readonly IdentityModule _module;

    public DeleteManagedPolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/identity/policies/managed/{PolicyId}");
        this.AccessControl("identity:ManagePolicies", _ => ResourceArn.AppUrn);
        Description(d => d.WithTags("Identity Policies"));
        Summary(new DeleteManagedPolicySummary());
    }

    public override async Task HandleAsync(DeleteManagedPolicyRequest req, CancellationToken ct)
    {
        var command = new DeleteManagedPolicyCommand(req.PolicyId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
