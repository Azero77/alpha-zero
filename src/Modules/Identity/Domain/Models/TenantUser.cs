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
    public Guid TenantId { get; private set; }
    public string IdentityId { get; private set; } = string.Empty; // The 'sub' from Cognito JWT
    public string Name { get; private set; } = string.Empty;
    public TenantUserDeviceInfo DeviceInfo;
    
    private TenantUser() { } // EF Core

    private TenantUser(Guid id, Guid tenantId, string identityId, string name, TenantUserDeviceInfo deviceInfo) : base(id)
    {
        TenantId = tenantId;
        IdentityId = identityId;
        Name = name;
        DeviceInfo = deviceInfo;
    }

    public static ErrorOr<TenantUser> Create(Guid tenantId, string identityId, string name, TenantUserDeviceInfo deviceInfo)
    {
        if (string.IsNullOrWhiteSpace(identityId)) return Error.Validation("User.IdentityId", "Identity ID is required.");
        return new TenantUser(Guid.NewGuid(), tenantId, identityId, name, deviceInfo);
    }

    public void LockUser()
    {
        DeviceInfo = DeviceInfo with { IsLocked  = true };
    }
}

public record TenantUserDeviceInfo(string DeviceId,DevicePlatform platform, bool IsLocked = false)
{
    public static TenantUserDeviceInfo Empty => new(string.Empty, DevicePlatform.Web, false);
}

public enum DevicePlatform
{
    Web,
    Android,
    Ios
}

