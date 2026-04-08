using AlphaZero.Modules.Identity.Application.Principals.Commands.CreatePrincipal;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Commands.CreatePrincipal;

public record CreatePrincipalRequest
{
    public string IdentityId { get; init; } = default!;
    public PrincipalType PrincipalType { get; init; }
    public string PrincipalScope { get; init; } = default!;
    public string Name { get; init; } = default!;
    public Guid? ResourceId { get; init; }
    public ResourceType? ScopeResourceType { get; init; }
}

public record CreatePrincipalResponse(Guid Id);

public class CreatePrincipalEndpoint : Endpoint<CreatePrincipalRequest, CreatePrincipalResponse>
{
    private readonly IdentityModule _module;

    public CreatePrincipalEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/principals");
        AllowAnonymous(); // Typically this would be restricted to Admin
        Description(d => d.WithTags("Identity"));
    }

    public override async Task HandleAsync(CreatePrincipalRequest req, CancellationToken ct)
    {
        var command = new CreatePrincipalCommand(req.IdentityId, req.PrincipalType, req.PrincipalScope, req.Name, req.ResourceId, req.ScopeResourceType);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/identity/principals/{result.Value}", responseBody: new CreatePrincipalResponse(result.Value), cancellation: ct);
    }
}
