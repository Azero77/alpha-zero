namespace AlphaZero.Shared.Infrastructure.Database;

public interface ITenantDbContext
{
    Guid? TenantId { get; }
}
