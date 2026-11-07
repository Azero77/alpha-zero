using AlphaZero.Modules.Library.Application.Analytics.Queries.GetFraudFlags;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryFunnel;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryRevenue;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;

namespace AlphaZero.Modules.Library.Application.Analytics;

/// <summary>
/// Application-owned analytics read contracts (issue #59). Implementation lives in Infrastructure and
/// reads the <c>LibraryAnalytics</c> projection, the audit stream, and (for provisioning state) the
/// Courses module via the cross-module bus contract.
/// </summary>
public interface ILibraryAnalyticsQueryService
{
    Task<LibraryFunnelDto> GetLibraryFunnelAsync(Guid? libraryId, CancellationToken cancellationToken = default);
    Task<LibraryRevenueDto> GetLibraryRevenueAsync(Guid? libraryId, CancellationToken cancellationToken = default);
    Task<List<FraudFlagDto>> GetFraudFlagsAsync(Guid? libraryId, CancellationToken cancellationToken = default);
    Task<List<FailedProvisioningDto>> GetProvisioningQueueAsync(Guid? libraryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Application-owned cross-module contract: reads the provisioning state (Courses module) of
/// requested access codes for the failed/pending provisioning queue (issue #59).
/// </summary>
public interface IProvisioningStateReadService
{
    Task<IReadOnlyList<FailedProvisioningDto>> GetProvisioningStateAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessCodeIds,
        CancellationToken cancellationToken = default);
}
