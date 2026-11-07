namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Append-only repository contract for the LibraryAuditLog stream.
/// Exposes add and read methods only — no mutation or deletion is permitted.
/// </summary>
public interface ILibraryAuditLogRepository
{
    Task AddAsync(LibraryAuditLog entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LibraryAuditLog>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LibraryAuditLog>> ListAsync(Guid tenantId, Guid? libraryId = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid tenantId, Guid? libraryId = null, CancellationToken cancellationToken = default);
}
