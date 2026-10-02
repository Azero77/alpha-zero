using Microsoft.AspNetCore.Http;

namespace AlphaZero.Shared.Authorization;

/// <summary>
/// Getting the Device in which the user has requested the resource
/// </summary>
public interface IDeviceProvider
{
    string? GetDeviceId();
}
