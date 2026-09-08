using AlphaZero.Modules.Tenants.Domain;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;

public record TenantDto(
    Guid Id,
    string Name,
    string Subdomain,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? DarkModeLogoUrl,
    string? FaviconUrl,
    string Status,
    DateTime CreatedAt);

public record GetTenantQuery(Guid Id) : IRequest<ErrorOr<TenantDto>>;

public sealed class GetTenantQueryHandler(ITenantRepository tenantRepository) : IRequestHandler<GetTenantQuery, ErrorOr<TenantDto>>
{
    public async Task<ErrorOr<TenantDto>> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetById(request.Id);
        if (tenant is null) return Error.NotFound("Tenant.NotFound", "Tenant not found.");

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.Branding.LogoUrl,
            tenant.Branding.PrimaryColor,
            tenant.Branding.SecondaryColor,
            tenant.Branding.DarkModeLogoUrl,
            tenant.Branding.FaviconUrl,
            tenant.Status.ToString(),
            tenant.CreatedAt);
    }
}
