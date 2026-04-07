using AlphaZero.Modules.Identity.Application.Principals.Commands.AttachManagedPolicy;
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

public class AttachManagedPolicyEndpoint : Endpoint<AttachManagedPolicyRequest>
{
    private readonly IdentityModule _module;

    public AttachManagedPolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}");
        AllowAnonymous();
        Description(d => d.WithTags("Identity"));
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
