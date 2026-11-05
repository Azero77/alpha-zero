using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Library.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="ILibraryQueryService"/> providing the caching decorator slot
/// following the Courses-module pattern (e.g. <c>CachedCourseQueryService</c>).
/// </summary>
#pragma warning disable CS9113 // Parameter is unread (caching decorator slot reserved for future cache policies)
public class CachedLibraryQueryService(
    ILibraryQueryService inner,
    ICachingService cache,
    ITenantProvider tenantProvider) : ILibraryQueryService
#pragma warning restore CS9113
{
    public Task<PagedResult<LibraryDto>> ListLibrariesAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        return inner.ListLibrariesAsync(page, perPage, cancellationToken);
    }

    public Task<LibraryDto?> GetLibraryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return inner.GetLibraryByIdAsync(id, cancellationToken);
    }

    public Task<PagedResult<RedemptionAuditLogDto>> GetRedemptionLogsAsync(
        Guid libraryId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return inner.GetRedemptionLogsAsync(libraryId, from, to, page, pageSize, cancellationToken);
    }
}
