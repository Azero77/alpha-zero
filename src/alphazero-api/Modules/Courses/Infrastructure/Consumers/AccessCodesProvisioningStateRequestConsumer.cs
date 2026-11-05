using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// MassTransit request-response consumer (issue #59): answers Library's provisioning
/// reconciliation queue with the FailedProvisioningRecords whose status is Pending or Failed
/// for the requested tenant/access codes. Tenant filtering is explicit (I-TENANT): the record
/// set is tenant-owned and consumer runs without a request tenant context.
/// </summary>
public class AccessCodesProvisioningStateRequestConsumer(
    AppDbContext dbContext,
    ILogger<AccessCodesProvisioningStateRequestConsumer> logger)
    : IConsumer<GetAccessCodesProvisioningStateRequest>
{
    public async Task Consume(ConsumeContext<GetAccessCodesProvisioningStateRequest> context)
    {
        var msg = context.Message;

        // Contract invariant (see GetAccessCodesProvisioningStateRequest): AccessCodeIds must be
        // non-empty — there is no tenant-wide fallback. An empty list answers empty so no
        // accidental cross-module data fan-out is possible (Library skips the request anyway).
        if (msg.AccessCodeIds.Count == 0)
        {
            await context.RespondAsync(new AccessCodesProvisioningStateResponse([]));
            return;
        }

        var ids = msg.AccessCodeIds.ToList();
        var query = dbContext.FailedProvisioningRecords
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == msg.TenantId
                        && r.Status != ProvisioningStatus.Provisioned
                        && ids.Contains(r.AccessCodeId));

        var records = await query
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var items = records.Select(r => new AccessCodeProvisioningStateItem(
            r.AccessCodeId,
            r.UserId,
            r.TargetResourceArn,
            r.Status.ToString(),
            r.FailureReason,
            r.RetryCount,
            r.CreatedAt,
            r.LastAttemptedAt)).ToList();

        logger.LogDebug(
            "Answered provisioning-state request for tenant {TenantId}: {Count} non-provisioned of {Requested} requested access codes.",
            msg.TenantId, items.Count, msg.AccessCodeIds.Count);

        await context.RespondAsync(new AccessCodesProvisioningStateResponse(items));
    }
}
