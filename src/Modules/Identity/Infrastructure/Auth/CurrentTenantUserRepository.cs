using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

public class CurrentTenantUserRepository(
    IHttpContextAccessor contextAccessor,
    IRepository<TenantUser> userRepository) : ICurrentTenantUserRepository
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
        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var tenantUserId))
        {
            return null;
        }

        // 2. Fetch the full TenantUser aggregate from DB
        var tenantUser = await userRepository.GetById(tenantUserId);
        if (tenantUser == null)
        {
            return null;
        }


        return new TenantUserDTO(
            tenantUser.Id,
            tenantUser.IdentityId,
            tenantUser.Name);
    }
}
