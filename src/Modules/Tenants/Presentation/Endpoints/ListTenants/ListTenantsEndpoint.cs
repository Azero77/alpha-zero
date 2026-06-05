using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.ListTenants;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.ListTenants;

public record ListTenantsRequest
{
    public string? Q { get; init; }
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListTenantsEndpoint(TenantsModule module) : Endpoint<ListTenantsRequest, PagedResult<TenantDto>>
{
    public override void Configure()
    {
        Get("/tenants");
        this.AccessControl("tenants:Manage", req => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Tenants"));
    }

    public override async Task HandleAsync(ListTenantsRequest req, CancellationToken ct)
    {
        var query = new ListTenantsQuery(req.Q, req.Page, req.PerPage);
        var result = await module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
