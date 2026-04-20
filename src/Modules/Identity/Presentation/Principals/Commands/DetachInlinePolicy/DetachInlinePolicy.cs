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
        this.AccessControl("identity:ManagePrincipals", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Identity"));
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
