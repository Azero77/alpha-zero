using AlphaZero.Shared.Authorization;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Infrastructure.Auth;

/// <summary>
/// Infrastructure implementation for reading the Device ID from the HTTP request headers.
/// </summary>
public class DeviceProvider(IHttpContextAccessor httpContextAccessor) : IDeviceProvider
{
    public string? GetDeviceId()
    {
        return httpContextAccessor.HttpContext?.Request.Headers["X-Device-Id"].ToString();
    }

    public void SetDeviceId(string deviceId)
    {
        // Headers are read-only in this context
    }
}
