namespace AlphaZero.Shared.Domain;

public interface IDomainTenantOwned
{
    Guid TenantId { get; }
}
