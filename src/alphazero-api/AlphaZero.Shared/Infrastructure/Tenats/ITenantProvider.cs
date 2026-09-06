using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AlphaZero.Shared.Infrastructure.Tenats;

public interface ITenantProvider
{
    Guid? GetTenant();
}
public class FakeTenantProvider : ITenantProvider
{
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid? GetTenant() => DefaultTenantId;
}

public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string TenantClaim = "TenantId";
    private const string TenantIdHeader = "X-TenantId";

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetTenant()
    {
        Claim? claim = _httpContextAccessor?.HttpContext?.User?.FindFirst(TenantClaim);
        if (claim is null || !Guid.TryParse(claim.Value,out Guid tenantIdFromClaim))
            return null;

        var tenantIdFromHeaderString = _httpContextAccessor?.HttpContext?.Request?.Headers[TenantIdHeader].FirstOrDefault();
        Guid.TryParse(tenantIdFromHeaderString, out Guid tenantIdFromHeader);
        //this happens when a global principal tries to reach a resource in a specific tenant, we don't care about the global tenantID (which is empty)
        //we care about the tenantID from the header, which is the one that matters for the resource being accessed
        //if the resource was global , then we read from the claim as usual , and the Tenant Provider would only call the specific resources for this
        if (tenantIdFromClaim == Guid.Empty && tenantIdFromHeader != Guid.Empty)
            return tenantIdFromHeader;

        if (tenantIdFromClaim != tenantIdFromHeader)
            return null;
        return tenantIdFromClaim;
    }

}