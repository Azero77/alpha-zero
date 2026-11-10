using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetAccessCodesProvisioningState;

/// <summary>
/// Read-only reconciliation query answering Library's provisioning reconciliation queue
/// (issue #59): returns the FailedProvisioningRecords whose status is Pending or Failed
/// for the requested tenant/access codes. Idempotency artifact: a code actually
/// provisioned in Courses either has a Provisioned record (filtered out here) or no
/// record at all — both mean "no failed provisioning entry" to Library.
/// Tenant filtering is explicit (I-TENANT): the record set is tenant-owned and this
/// query runs without a request tenant context, so the repository lookup bypasses
/// global query filters and matches on TenantId itself.
/// </summary>
public record GetAccessCodesProvisioningStateQuery(
    Guid TenantId,
    IReadOnlyList<Guid> AccessCodeIds) : IQuery<List<AccessCodeProvisioningStateItem>>;

public class GetAccessCodesProvisioningStateQueryValidator : AbstractValidator<GetAccessCodesProvisioningStateQuery>
{
    public GetAccessCodesProvisioningStateQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        // Contract invariant (see GetAccessCodesProvisioningStateRequest): AccessCodeIds must
        // be non-empty — there is no tenant-wide fallback. The originating consumer answers
        // an empty list with an empty response before ever dispatching this query, so this
        // rule is defense in depth against accidental cross-module data fan-out.
        RuleFor(x => x.AccessCodeIds).NotEmpty();
    }
}

public sealed class GetAccessCodesProvisioningStateQueryHandler(
    IFailedProvisioningRecordRepository failedProvisioningRepository,
    ILogger<GetAccessCodesProvisioningStateQueryHandler> logger)
    : IRequestHandler<GetAccessCodesProvisioningStateQuery, ErrorOr<List<AccessCodeProvisioningStateItem>>>
{
    public async Task<ErrorOr<List<AccessCodeProvisioningStateItem>>> Handle(
        GetAccessCodesProvisioningStateQuery request,
        CancellationToken cancellationToken)
    {
        var records = await failedProvisioningRepository.GetUnprovisionedByTenantAndAccessCodesAsync(
            request.TenantId,
            request.AccessCodeIds,
            cancellationToken);

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
            request.TenantId, items.Count, request.AccessCodeIds.Count);

        return items;
    }
}
