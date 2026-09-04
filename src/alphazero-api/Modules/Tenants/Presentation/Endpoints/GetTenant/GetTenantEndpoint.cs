using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Tenants.Presentation.Endpoints.GetTenant;

public record GetTenantRequest { public Guid Id { get; init; } }

public class GetTenantEndpoint(TenantsModule module) : Endpoint<GetTenantRequest, TenantDto>
{
    public override void Configure()
    {
        Get("/tenants/{Id}");
        this.AccessControl("tenants:Manage", req => ResourceArn.ForTenant(req.Id));
        Description(d => d.WithTags("Tenants"));
    }

    public override async Task HandleAsync(GetTenantRequest req, CancellationToken ct)
    {
        var query = new GetTenantQuery(req.Id);
        var result = await module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
