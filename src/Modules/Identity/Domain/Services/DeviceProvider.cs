using AlphaZero.Shared.Authorization;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Domain.Services;

/// <summary>
/// Reading X-Device-Id header and making it available for the authorization context to evaluate device-based access control policies.
/// </summary>
public class DeviceProvider(IHttpContextAccessor httpContext) : IDeviceProvider
{
    public string? GetDeviceId()
    {
        return httpContext.HttpContext?.Request.Headers["X-Device-Id"].ToString();
    }
}
