using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

public class CurrentTenantUserRepository(
    IHttpContextAccessor contextAccessor) : ICurrentTenantUserRepository
{
    public Task<TenantUserDTO?> GetCurrentUser()
    {
        var user = contextAccessor.HttpContext?.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult<TenantUserDTO?>(null);
        }

        return Task.FromResult(user.ToTenantUserDTO());
    }
}

