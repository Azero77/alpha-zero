using AlphaZero.Modules.Identity.Application.Principals.Commands.AttachInlinePolicy;
using AlphaZero.Modules.Identity.Domain.Models;
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
        AllowAnonymous();
        Description(d => d.WithTags("Identity"));
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
