using AlphaZero.Modules.Library.Application.Analytics;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetFraudFlags;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryFunnel;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryRevenue;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Library.Infrastructure.Caching;

/// <summary>
/// Decorator over <see cref="ILibraryAnalyticsQueryService"/> providing the caching decorator slot
/// following the Courses-module pattern (e.g. <c>CachedCourseQueryService</c>).
/// </summary>
#pragma warning disable CS9113 // Parameter is unread (caching decorator slot reserved for future cache policies)
public class CachedLibraryAnalyticsQueryService(
    ILibraryAnalyticsQueryService inner,
    ICachingService cache,
    ITenantProvider tenantProvider) : ILibraryAnalyticsQueryService
#pragma warning restore CS9113
{
    public Task<LibraryFunnelDto> GetLibraryFunnelAsync(Guid? libraryId, CancellationToken cancellationToken = default)
        => inner.GetLibraryFunnelAsync(libraryId, cancellationToken);

    public Task<LibraryRevenueDto> GetLibraryRevenueAsync(Guid? libraryId, CancellationToken cancellationToken = default)
        => inner.GetLibraryRevenueAsync(libraryId, cancellationToken);

    public Task<List<FraudFlagDto>> GetFraudFlagsAsync(Guid? libraryId, CancellationToken cancellationToken = default)
        => inner.GetFraudFlagsAsync(libraryId, cancellationToken);

    public Task<List<FailedProvisioningDto>> GetProvisioningQueueAsync(Guid? libraryId, CancellationToken cancellationToken = default)
        => inner.GetProvisioningQueueAsync(libraryId, cancellationToken);
}
