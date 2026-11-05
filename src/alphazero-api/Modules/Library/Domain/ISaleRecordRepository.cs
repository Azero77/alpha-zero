namespace AlphaZero.Modules.Library.Domain;

public interface ISaleRecordRepository
{
    Task AddAsync(SaleRecord saleRecord, CancellationToken cancellationToken = default);
    Task<bool> ExistsForCodeAsync(Guid accessCodeId, CancellationToken cancellationToken = default);
    Task<SaleRecord?> GetByAccessCodeIdAsync(Guid accessCodeId, CancellationToken cancellationToken = default);
    Task<int> CountByLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default);
    Task<decimal> GetRevenueByLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default);
}
