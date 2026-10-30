using AlphaZero.Shared.Domain;
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
    private readonly IAmbientTenantContext _ambientTenant;
    private const string TenantClaim = "tid";
    private const string TenantIdHeader = "X-TenantId";

    private Guid? _cachedTenantId = null;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor, IAmbientTenantContext ambientTenant)
    {
        _httpContextAccessor = httpContextAccessor;
        _ambientTenant = ambientTenant;
    }

    public Guid? GetTenant()
    {
        if (_cachedTenantId is not null)
            return _cachedTenantId;

        // Tenant precedence (#32/D2):
        //   1. A parseable, non-empty `tid` claim is the tenant authority — the token wins,
        //      even if a header disagrees (the old mismatch→null voiding branch is gone).
        //   2. A claim that is absent, empty, or Guid.Empty (a global principal, which is
        //      NOT a tenant identity) defers to the X-TenantId header. This is also what
        //      lets anonymous callers resolve a tenant for the catalog/view surfaces.
        //   3. No usable claim and no parseable header → null: fail closed (empty results /
        //      404 masks), never a guessed tenant.
        var claim = _httpContextAccessor?.HttpContext?.User?.FindFirst(TenantClaim);
        if (claim is not null && Guid.TryParse(claim.Value, out var tenantIdFromClaim) && tenantIdFromClaim != ResourceArn.GlobalTenantGuid)
        {
            _cachedTenantId = tenantIdFromClaim;
            return tenantIdFromClaim;
        }

        var tenantIdFromHeaderString = _httpContextAccessor?.HttpContext?.Request?.Headers[TenantIdHeader].FirstOrDefault();
        if (Guid.TryParse(tenantIdFromHeaderString, out var tenantIdFromHeader) && tenantIdFromHeader != ResourceArn.GlobalTenantGuid)
        {
            _cachedTenantId = tenantIdFromHeader;
            return tenantIdFromHeader;
        }

        // Message consumers and background work have no HTTP context; the consume filter
        // publishes the message tenant through the ambient context before handlers run.
        if (_ambientTenant.TenantId is { } tenantIdFromAmbient)
        {
            _cachedTenantId = tenantIdFromAmbient;
            return tenantIdFromAmbient;
        }

        return null;
    }

}