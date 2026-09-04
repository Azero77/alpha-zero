using AlphaZero.Modules.Tenants.Application.Tenants.Commands.UpdateTenant;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.UpdateTenant;

public record UpdateTenantRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
}

public class UpdateTenantEndpoint(TenantsModule module) : Endpoint<UpdateTenantRequest>
{
    public override void Configure()
    {
        Put("/tenants/{Id}");
        this.AccessControl("tenants:Manage", req => ResourceArn.ForTenant(req.Id));
        Description(d => d.WithTags("Tenants"));
    }

    public override async Task HandleAsync(UpdateTenantRequest req, CancellationToken ct)
    {
        var command = new UpdateTenantCommand(req.Id, req.Name, req.LogoUrl, req.PrimaryColor, req.SecondaryColor);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
