using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

/// <summary>
/// The central anchor for a User within a Tenant.
/// Holds the active session state and base tenant info.
/// </summary>
public class TenantUser : AggregateRoot, IDomainTenantOwned
{
    public Guid TenantId { get; private set; }
    public string IdentityId { get; private set; } = string.Empty; // The 'sub' from JWT
    public string Name { get; private set; } = string.Empty;
    
    // The "Single Device" enforcer
    public Guid ActiveSessionId { get; private set; }

    private TenantUser() { } // EF Core

    private TenantUser(Guid id, Guid tenantId, string identityId, string name) : base(id)
    {
        TenantId = tenantId;
        IdentityId = identityId;
        Name = name;
        ActiveSessionId = Guid.NewGuid();
    }

    public static ErrorOr<TenantUser> Create(Guid tenantId, string identityId, string name)
    {
        if (string.IsNullOrWhiteSpace(identityId)) return Error.Validation("User.IdentityId", "Identity ID is required.");
        return new TenantUser(Guid.NewGuid(), tenantId, identityId, name);
    }

    /// <summary>
    /// Called during login to invalidate all other device sessions.
    /// </summary>
    public Guid RefreshSession()
    {
        ActiveSessionId = Guid.NewGuid();
        return ActiveSessionId;
    }
}
