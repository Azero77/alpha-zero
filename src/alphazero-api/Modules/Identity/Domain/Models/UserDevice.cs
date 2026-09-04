using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class UserDevice : Entity
{
    public Guid TenantUserId { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; }
    public string PublicKey { get; private set; } = string.Empty;
    public DateTime RegisteredAt { get; private set; }

    private UserDevice() { } // EF Core

    private UserDevice(Guid id, Guid tenantUserId, string deviceName, DevicePlatform platform, string publicKey, DateTime registeredAt) 
        : base(id)
    {
        TenantUserId = tenantUserId;
        DeviceName = deviceName;
        Platform = platform;
        PublicKey = publicKey;
        RegisteredAt = registeredAt;
    }

    public static UserDevice Create(Guid tenantUserId, string deviceName, DevicePlatform platform, string publicKey, DateTime registeredAt)
    {
        return new UserDevice(Guid.NewGuid(), tenantUserId, deviceName, platform, publicKey, registeredAt);
    }
}
