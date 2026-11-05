using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Library.Application.Analytics;
using AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure adapter implementing <see cref="IProvisioningStateReadService"/> using MassTransit's
/// request-response pattern over the module bus (established cross-module read precedent:
/// <c>CoursePlanPrincipalService</c>). No direct Library→Courses dependency on domain/DB — only on
/// the Courses integration-event message contract.
/// </summary>
public class BusProvisioningStateService(
    IBus moduleBus,
    ILogger<BusProvisioningStateService> logger) : IProvisioningStateReadService
{
    public async Task<IReadOnlyList<FailedProvisioningDto>> GetProvisioningStateAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessCodeIds,
        CancellationToken cancellationToken = default)
    {
        var requestClient = moduleBus.CreateRequestClient<GetAccessCodesProvisioningStateRequest>(
            TimeSpan.FromSeconds(10));

        var response = await requestClient.GetResponse<AccessCodesProvisioningStateResponse>(
            new GetAccessCodesProvisioningStateRequest(tenantId, accessCodeIds.ToList()),
            cancellationToken);

        logger.LogDebug(
            "Provisioning reconciliation read: tenant {TenantId}, {Requested} codes requested, {Found} non-provisioned records.",
            tenantId, accessCodeIds.Count, response.Message.Items.Count);

        return response.Message.Items
            .Select(i => new FailedProvisioningDto(
                i.AccessCodeId,
                i.UserId,
                i.TargetResourceArn,
                i.Status,
                i.FailureReason,
                i.RetryCount,
                i.CreatedAt,
                i.LastAttemptedAt,
                LibraryId: null,
                RedeemedAt: null))
            .ToList();
    }
}
