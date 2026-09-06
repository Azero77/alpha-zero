using AlphaZero.Modules.Identity.Application.Principals.Commands.CreatePrincipal;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Commands.CreatePrincipal;

public record CreatePrincipalRequest
{
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string PrincipalType { get; init; } = default!;
    public string? PrincipalScope { get; init; }
    public string Name { get; init; } = default!;
}

public record CreatePrincipalResponse(Guid Id);

public class CreatePrincipalSummary : Summary<CreatePrincipalEndpoint>
{
    public CreatePrincipalSummary()
    {
        Summary = "Creates a new principal";
        Description = "Creates an IAM principal (User, Role, or System) within the tenant.";
        Response<CreatePrincipalResponse>(201, "Principal created successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Username empty, Password < 8 chars, Name empty/too long, PrincipalType invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant not found or invalid credentials)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
    }
}

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
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new CreatePrincipalSummary());
    }

    public override async Task HandleAsync(CreatePrincipalRequest req, CancellationToken ct)
    {
        var command = new CreatePrincipalCommand(req.Username, req.Password, req.PrincipalType, req.PrincipalScope, req.Name);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/identity/principals/{result.Value}", responseBody: new CreatePrincipalResponse(result.Value), cancellation: ct);
    }
}
