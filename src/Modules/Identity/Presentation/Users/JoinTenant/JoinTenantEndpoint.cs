using AlphaZero.Modules.Identity.Application.Users.Commands.JoinTenant;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Users.JoinTenant;

public record JoinTenantRequest
{
    public Guid TenantId { get; init; }
}

public class JoinTenantEndpoint(IdentityModule module) : Endpoint<JoinTenantRequest>
{
    public override void Configure()
    {
        Post("/identity/tenants/{TenantId}/join");
        // Requires Global Auth (e.g. Cognito JWT)
        // No specific tenant permission required as this is the action to join.
        Description(d => d
            .WithTags("Identity Users")
            .Produces(204)
            .ProducesProblemDetails(400)
            .ProducesProblemDetails(401));
    }

    public override async Task HandleAsync(JoinTenantRequest req, CancellationToken ct)
    {
        // Extract IdentityId from Global JWT
        var identityId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var name = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "New User";

        if (string.IsNullOrEmpty(identityId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var command = new JoinTenantCommand(req.TenantId, identityId, name, "device1","Web");
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
