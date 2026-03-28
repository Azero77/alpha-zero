namespace AlphaZero.Shared.Infrastructure.Tenats;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
public class TenantOwned : ITenantOwned
{
    public Guid TenantId { get; set; }
}