using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Pipes;
using System.Security.Claims;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

public class CurrentTenantUserRepository(
    IHttpContextAccessor contextAccessor) : ICurrentTenantUserRepository
{
    public async Task<TenantUserDTO?> GetCurrentUser()
    {
        var user = contextAccessor.HttpContext?.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            return null;
        }

        // 1. Extract the TenantUserId from 'sub' claim
        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        var nameClaim = user.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
        var identityIdClaim = user.FindFirst("identity_id")?.Value;
        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var tenantUserId)
            || nameClaim is null || identityIdClaim is null)
        {
            return null;
        }


        return new TenantUserDTO(
            tenantUserId,
            identityIdClaim,
            nameClaim);
    }
}
