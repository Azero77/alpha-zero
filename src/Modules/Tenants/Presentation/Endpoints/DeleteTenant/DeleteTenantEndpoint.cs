using AlphaZero.Modules.Tenants.Application.Tenants.Commands.DeleteTenant;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.DeleteTenant;

public record DeleteTenantRequest { public Guid Id { get; init; } }

public class DeleteTenantEndpoint(TenantsModule module) : Endpoint<DeleteTenantRequest>
{
    public override void Configure()
    {
        Delete("/tenants/{Id}");
        this.AccessControl("tenants:Manage", req => ResourceArn.ForTenant(req.Id));
        Description(d => d.WithTags("Tenants"));
    }

    public override async Task HandleAsync(DeleteTenantRequest req, CancellationToken ct)
    {
        var command = new DeleteTenantCommand(req.Id);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
