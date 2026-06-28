using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Tenants.Domain;

public class Tenant : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Subdomain { get; private set; } = default!;
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Tenant() { }

    private Tenant(
        Guid id,
        string name,
        string subdomain,
        string? logoUrl,
        string? primaryColor,
        string? secondaryColor,
        TenantStatus status,
        DateTime createdAt) : base(id)
    {
        Name = name;
        Subdomain = subdomain;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Tenant Create(
        string name,
        string subdomain,
        string? logoUrl = null,
        string? primaryColor = null,
        string? secondaryColor = null)
    {
        var tenant = new Tenant(
            Guid.NewGuid(),
            name,
            subdomain.ToLowerInvariant(),
            logoUrl,
            primaryColor,
            secondaryColor,
            TenantStatus.Active,
            DateTime.UtcNow);

        tenant.AddDomainEvent(new TenantCreatedDomainEvent(tenant.Id, tenant.Name, tenant.Subdomain));

        return tenant;
    }

    public void UpdateDetails(string name, string? logoUrl)
    {
        Name = name;
        LogoUrl = logoUrl;
    }

    public void UpdateTheme(string? primaryColor, string? secondaryColor)
    {
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
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

public class TenantSuspendedDomainEvent : DomainEvent
{
    public Guid TenantId { get; }

    public TenantSuspendedDomainEvent(Guid tenantId)
    {
        TenantId = tenantId;
    }
}