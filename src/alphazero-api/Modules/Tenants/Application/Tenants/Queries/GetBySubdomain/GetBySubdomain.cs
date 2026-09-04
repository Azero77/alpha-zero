using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetBySubdomain;

public record GetTenantBySubdomainQuery(string Subdomain) : IRequest<ErrorOr<TenantDto>>;

public sealed class GetTenantBySubdomainQueryHandler(ITenantRepository tenantRepository) : IRequestHandler<GetTenantBySubdomainQuery, ErrorOr<TenantDto>>
{
    public async Task<ErrorOr<TenantDto>> Handle(GetTenantBySubdomainQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySubdomainAsync(request.Subdomain, cancellationToken);
        
        if (tenant is null) 
            return Error.NotFound("Tenant.NotFound", $"No academy found for subdomain '{request.Subdomain}'.");

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.Status.ToString(),
            tenant.CreatedAt);
    }
}
