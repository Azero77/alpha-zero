using AlphaZero.Modules.Identity.Application.Principals.Commands.DetachManagedPolicy;
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
        AllowAnonymous();
        Description(d => d.WithTags("Identity"));
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
