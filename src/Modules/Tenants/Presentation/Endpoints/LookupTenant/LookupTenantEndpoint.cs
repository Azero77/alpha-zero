using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetBySubdomain;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.LookupTenant;

public record LookupTenantRequest { public string Subdomain { get; init; } = default!; }

public class LookupTenantEndpoint(TenantsModule module) : Endpoint<LookupTenantRequest, TenantDto>
{
    public override void Configure()
    {
        Get("/tenants/lookup");
        AllowAnonymous();
        Description(d => d
            .WithTags("Tenants")
            .Produces<TenantDto>(200)
            .ProducesProblemDetails(404));
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

        await Send.OkAsync(result.Value, ct);
    }
}
