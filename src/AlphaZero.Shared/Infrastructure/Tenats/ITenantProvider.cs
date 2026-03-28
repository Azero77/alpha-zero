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

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetTenant()
    {

        Claim? claim = _httpContextAccessor?.HttpContext?.User?.FindFirst(TenantClaim);
        if (claim is null || !Guid.TryParse(claim.Value,out Guid tenantId))
            return null;
        return tenantId;
    }
}