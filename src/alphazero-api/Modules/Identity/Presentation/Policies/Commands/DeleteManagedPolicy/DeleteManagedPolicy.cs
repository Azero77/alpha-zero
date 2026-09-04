using AlphaZero.Modules.Identity.Application.Policies.Commands.DeleteManagedPolicy;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Policies.Commands.DeleteManagedPolicy;

public record DeleteManagedPolicyRequest { public Guid PolicyId { get; init; } }

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
