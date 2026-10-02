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
    public string? DarkModeLogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
}

public record CreateTenantResponse(Guid Id);

public class CreateTenantSummary : Summary<CreateTenantEndpoint>
{
    public CreateTenantSummary()
    {
        Summary = "Creates a new academy tenant";
        Description = "Provisions an isolated tenant/academy instance with its own subdomain and branding.";
        Response<CreateTenantResponse>(201, "Tenant created successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Name empty/too long, Subdomain invalid, Invalid hex colors)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing tenants:Manage permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Subdomain already taken (Tenant.SubdomainNotUnique)");
    }
}

public class CreateTenantEndpoint(TenantsModule module) : Endpoint<CreateTenantRequest, CreateTenantResponse>
{
    public override void Configure()
    {
        Post("/tenants");
        // Global Tenants Manager permission
        this.AccessControl("tenants:Manage", req => ResourceArn.AppUrn);
        Description(d => d.WithTags("Tenants"));
        Summary(new CreateTenantSummary());
    }

    public override async Task HandleAsync(CreateTenantRequest req, CancellationToken ct)
    {
        var command = new CreateTenantCommand(
            req.Name,
            req.Subdomain,
            req.LogoUrl,
            req.PrimaryColor,
            req.SecondaryColor,
            req.DarkModeLogoUrl,
            req.FaviconUrl);

        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/tenants/{result.Value}", new CreateTenantResponse(result.Value), cancellation: ct);
    }
}
