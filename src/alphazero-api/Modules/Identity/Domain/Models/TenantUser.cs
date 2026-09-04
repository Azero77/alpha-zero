using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

/// <summary>
/// The central anchor for a User within a Tenant.
/// Holds the device state and base tenant info for authorization context.
/// </summary>
public class TenantUser : AggregateRoot, IDomainTenantOwned
{
    private readonly List<UserDevice> _devices = new();
    public Guid TenantId { get; private set; }
    public string IdentityId { get; private set; } = string.Empty; // The 'sub' from Cognito JWT
    public string Name { get; private set; } = string.Empty;
    public Guid? MainDeviceId { get; private set; }
    public DateTime? LastMainDeviceSwitchDate { get; private set; }
    public IReadOnlyCollection<UserDevice> Devices => _devices.AsReadOnly();
    
    private TenantUser() { } // EF Core

    private TenantUser(Guid id, Guid tenantId, string identityId, string name) : base(id)
    {
        TenantId = tenantId;
        IdentityId = identityId;
        Name = name;
    }

    public static ErrorOr<TenantUser> Create(Guid tenantId, string identityId, string name)
    {
        if (string.IsNullOrWhiteSpace(identityId)) return Error.Validation("User.IdentityId", "Identity ID is required.");
        return new TenantUser(Guid.NewGuid(), tenantId, identityId, name);
    }

    public ErrorOr<Success> RegisterDevice(string deviceName, DevicePlatform platform, string publicKey, DateTime now)
    {
        if (_devices.Any(d => d.PublicKey == publicKey))
            return Error.Conflict("Device.Exists", "This device is already registered.");

        var device = UserDevice.Create(Id, deviceName, platform, publicKey, now);
        _devices.Add(device);
        return Result.Success;
    }

    public ErrorOr<Success> SetMainDevice(Guid deviceId, DateTime now, bool skipCooldown = false)
    {
        if (_devices.All(d => d.Id != deviceId))
            return Error.NotFound("Device.NotFound", "Device not found in user's registered devices.");

        if (!skipCooldown && LastMainDeviceSwitchDate.HasValue && (now - LastMainDeviceSwitchDate.Value).TotalDays < 90)
        {
            return Error.Validation("Device.Cooldown", $"Main device can only be switched every 90 days. Next switch available in {90 - (now - LastMainDeviceSwitchDate.Value).TotalDays:F0} days.");
        }

        MainDeviceId = deviceId;
        LastMainDeviceSwitchDate = now;
        return Result.Success;
    }
}

public enum DevicePlatform
{
    Web,
    Android,
    Ios
}

