using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Tenants.Domain;

public class Tenant : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Subdomain { get; private set; } = default!;
    public TenantBranding Branding { get; private set; } = default!;
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Tenant() { }

    private Tenant(
        Guid id,
        string name,
        string subdomain,
        TenantBranding branding,
        TenantStatus status,
        DateTime createdAt) : base(id)
    {
        Name = name;
        Subdomain = subdomain;
        Branding = branding;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Tenant Create(
        string name,
        string subdomain,
        TenantBranding? branding = null)
    {
        var tenant = new Tenant(
            Guid.NewGuid(),
            name,
            subdomain.ToLowerInvariant(),
            branding ?? TenantBranding.Default,
            TenantStatus.Active,
            DateTime.UtcNow);

        tenant.AddDomainEvent(new TenantCreatedDomainEvent(tenant.Id, tenant.Name, tenant.Subdomain));

        return tenant;
    }

    public void UpdateDetails(string name)
    {
        Name = name;
    }

    public ErrorOr<Success> UpdateBranding(TenantBranding branding)
    {
        Branding = branding;
        AddDomainEvent(new TenantBrandingUpdatedDomainEvent(Id, Subdomain, Branding));
        return Result.Success;
    }

    public ErrorOr<Success> Suspend()
    {
        if (Status == TenantStatus.Suspended)
            return Error.Conflict("Tenant.AlreadySuspended", "Tenant is already suspended.");

        Status = TenantStatus.Suspended;
        AddDomainEvent(new TenantSuspendedDomainEvent(Id));
        return Result.Success;
    }

    public ErrorOr<Success> Activate()
    {
        if (Status == TenantStatus.Active)
            return Error.Conflict("Tenant.AlreadyActive", "Tenant is already active.");

        Status = TenantStatus.Active;
        return Result.Success;
    }
}

public class TenantCreatedDomainEvent : DomainEvent
{
    public Guid TenantId { get; }
    public string Name { get; }
    public string Subdomain { get; }

    public TenantCreatedDomainEvent(Guid tenantId, string name, string subdomain)
    {
        TenantId = tenantId;
        Name = name;
        Subdomain = subdomain;
    }
}

public class TenantBrandingUpdatedDomainEvent : DomainEvent
{
    public Guid TenantId { get; }
    public string Subdomain { get; }
    public TenantBranding Branding { get; }

    public TenantBrandingUpdatedDomainEvent(Guid tenantId, string subdomain, TenantBranding branding)
    {
        TenantId = tenantId;
        Subdomain = subdomain;
        Branding = branding;
    }
}

public class TenantSuspendedDomainEvent : DomainEvent
{
    public Guid TenantId { get; }

    public TenantSuspendedDomainEvent(Guid tenantId)
    {
        TenantId = tenantId;
    }
}