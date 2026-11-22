using AlphaZero.Modules.Payments.Domain.Catalog;

namespace AlphaZero.Modules.Payments.Application.Catalog;

public interface ICatalogProductRepository
{
    Task<Product?> FindBySourceIdentityAsync(Guid tenantId, string itemType, Guid itemId, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> GetCatalogSnapshotAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> ApplyCatalogSnapshotAsync(IReadOnlyList<CatalogProductSnapshot> sourceSnapshot,
        CancellationToken cancellationToken);
}
