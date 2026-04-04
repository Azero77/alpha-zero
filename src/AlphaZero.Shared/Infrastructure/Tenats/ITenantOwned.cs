using AlphaZero.Shared.Domain;

namespace AlphaZero.Shared.Infrastructure.Tenats;

public interface ITenantOwned : IDomainTenantOwned
{
    new Guid TenantId { get; set; }
}
public class TenantOwned : ITenantOwned
{
    public Guid TenantId { get; set; }
}

public class TenantOwnedEntity : Entity, IDomainTenantOwned
{
    public Guid TenantId { get; private set; }

    protected TenantOwnedEntity(Guid id, Guid tenantId) : base(id)
    {
        TenantId = tenantId;
    }
}

public class TenantOwnedAggregate : AggregateRoot, IDomainTenantOwned
{
    public Guid TenantId { get; private set; }

    protected TenantOwnedAggregate(Guid id, Guid tenantId) : base(id)
    {
        TenantId = tenantId;
    }
}