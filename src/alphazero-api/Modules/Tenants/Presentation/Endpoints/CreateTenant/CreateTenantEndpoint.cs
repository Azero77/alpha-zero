using AlphaZero.Modules.Tenants.Application.Tenants.Commands.CreateTenant;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.CreateTenant;

public record CreateTenantRequest
{
    public string Name { get; init; } = default!;
    public string Subdomain { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
}

public record CreateTenantResponse(Guid Id);

public class CreateTenantEndpoint(TenantsModule module) : Endpoint<CreateTenantRequest, CreateTenantResponse>
{
    public override void Configure()
    {
        Post("/tenants");
        // Global Tenants Manager permission
        this.AccessControl("tenants:Manage", req => ResourceArn.AppUrn);
        Description(d => d.WithTags("Tenants"));
    }

    public override async Task HandleAsync(CreateTenantRequest req, CancellationToken ct)
    {
        var command = new CreateTenantCommand(req.Name, req.Subdomain, req.LogoUrl, req.PrimaryColor, req.SecondaryColor);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/tenants/{result.Value}", new CreateTenantResponse(result.Value), cancellation: ct);
    }
}
