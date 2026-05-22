using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Queries.ListTenants;

public record ListTenantsQuery(string? Search = null, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<TenantDto>>>;

public sealed class ListTenantsQueryHandler(ITenantRepository tenantRepository) : IRequestHandler<ListTenantsQuery, ErrorOr<PagedResult<TenantDto>>>
{
    public async Task<ErrorOr<PagedResult<TenantDto>>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var result = await tenantRepository.Get(
            request.Page, 
            request.PerPage, 
            filter: t => string.IsNullOrEmpty(request.Search) || t.Name.Contains(request.Search) || t.Subdomain.Contains(request.Search),
            orderBy: t => t.Name,
            token: cancellationToken);

        var dtos = result.Items.Select(tenant => new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.Status.ToString(),
            tenant.CreatedAt)).ToList();

        return new PagedResult<TenantDto>(dtos, result.TotalCount, result.CurrentPage, result.PageSize);
    }
}
