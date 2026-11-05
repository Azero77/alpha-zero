using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Library.Application.Queries;

public interface ILibraryQueryService
{
    Task<PagedResult<LibraryDto>> ListLibrariesAsync(int page, int perPage, CancellationToken cancellationToken = default);
    Task<LibraryDto?> GetLibraryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RedemptionAuditLogDto>> GetRedemptionLogsAsync(
        Guid libraryId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
