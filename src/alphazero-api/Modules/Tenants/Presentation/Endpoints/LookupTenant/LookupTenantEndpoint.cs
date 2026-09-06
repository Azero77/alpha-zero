using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetBySubdomain;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.LookupTenant;

public record LookupTenantRequest { public string Subdomain { get; init; } = default!; }

public record LookupTenantBranding(string? PrimaryColor, string? SecondaryColor, string? LogoUrl);
public record LookupTenantResponse(Guid Id, string Subdomain, string Name, LookupTenantBranding Branding);

public class LookupTenantSummary : Summary<LookupTenantEndpoint>
{
    public LookupTenantSummary()
    {
        Summary = "Looks up tenant by subdomain";
        Description = "Publicly resolves academy branding and metadata based on the subdomain.";
        Response<LookupTenantResponse>(200, "Tenant details and branding retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Tenant not found (Tenant.NotFound)");
    }
}

public class LookupTenantEndpoint(TenantsModule module) : Endpoint<LookupTenantRequest, LookupTenantResponse>
{
    public override void Configure()
    {
        Get("/tenants/lookup");
        AllowAnonymous();
        Description(d => d.WithTags("Tenants"));
        Summary(new LookupTenantSummary());
    }

    public override async Task HandleAsync(LookupTenantRequest req, CancellationToken ct)
    {
        var query = new GetTenantBySubdomainQuery(req.Subdomain);
        var result = await module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var tenant = result.Value;
        var response = new LookupTenantResponse(
            tenant.Id,
            tenant.Subdomain,
            tenant.Name,
            new LookupTenantBranding(tenant.PrimaryColor, tenant.SecondaryColor, tenant.LogoUrl)
        );

        await Send.OkAsync(response, ct);
    }
}
