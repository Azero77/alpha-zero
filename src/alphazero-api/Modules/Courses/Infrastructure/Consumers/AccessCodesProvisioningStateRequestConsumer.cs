using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetAccessCodesProvisioningState;
using AlphaZero.Modules.Courses.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Thin MassTransit adapter over <see cref="GetAccessCodesProvisioningStateQuery"/> (issue #59).
/// Guards the contract invariant (non-empty AccessCodeIds) and delegates the record lookup to
/// the Courses application layer, which owns the business rules; the query result is translated
/// directly into the wire response.
/// </summary>
public class AccessCodesProvisioningStateRequestConsumer(
    ICoursesModule coursesModule,
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

        var query = new GetAccessCodesProvisioningStateQuery(msg.TenantId, msg.AccessCodeIds);
        var result = await coursesModule.Send(query, context.CancellationToken);

        if (result.IsError)
        {
            logger.LogError(
                "Provisioning-state query failed for tenant {TenantId}. Errors: {Errors}",
                msg.TenantId,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            await context.RespondAsync(new AccessCodesProvisioningStateResponse([]));
            return;
        }

        await context.RespondAsync(new AccessCodesProvisioningStateResponse(result.Value));
    }
}
