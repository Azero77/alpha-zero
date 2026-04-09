using AlphaZero.Modules.Identity.Application.Policies.Commands.CreateManagedPolicy;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Policies.Commands.CreateManagedPolicy;

public record CreateManagedPolicyRequest
{
    public string Name { get; init; } = default!;
    public List<PolicyTemplateStatement> Statements { get; init; } = new();
}

public record CreateManagedPolicyResponse(Guid Id);

public class CreateManagedPolicyEndpoint : Endpoint<CreateManagedPolicyRequest, CreateManagedPolicyResponse>
{
    private readonly IdentityModule _module;

    public CreateManagedPolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/policies/managed");
        AllowAnonymous();
        Description(d => d.WithTags("Identity Policies"));
    }

    public override async Task HandleAsync(CreateManagedPolicyRequest req, CancellationToken ct)
    {
        var command = new CreateManagedPolicyCommand(req.Name, req.Statements);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/identity/policies/managed/{result.Value}", responseBody: new CreateManagedPolicyResponse(result.Value), cancellation: ct);
    }
}
