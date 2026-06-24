using AlphaZero.Modules.Tenants.Application.Queries;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Queries.ListTenants;

public record ListTenantsQuery(string? Search = null, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<TenantDto>>>;

public sealed class ListTenantsQueryHandler(ITenantQueryService tenantQueryService) : IRequestHandler<ListTenantsQuery, ErrorOr<PagedResult<TenantDto>>>
{
    public async Task<ErrorOr<PagedResult<TenantDto>>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        return await tenantQueryService.ListTenantsAsync(request.Search, request.Page, request.PerPage, cancellationToken);
    }
}
