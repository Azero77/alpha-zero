namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Request-response contract (issue #59): Library asks Courses for the provisioning state of
/// its activated access codes. Courses answers with the non-Provisioned records that exist in
/// its FailedProvisioningRecords table; codes without a record are simply absent from the
/// response (Library treats them as having no failed provisioning entry).
/// Handled asynchronously via MassTransit request-response over the module bus.
/// </summary>
public record GetAccessCodesProvisioningStateRequest(Guid TenantId, IReadOnlyList<Guid> AccessCodeIds);

/// <summary>State of one non-provisioned access code in Courses (Pending or Failed).</summary>
public record AccessCodeProvisioningStateItem(
    Guid AccessCodeId,
    Guid UserId,
    string TargetResourceArn,
    string Status,
    string? FailureReason,
    int RetryCount,
    DateTime CreatedAt,
    DateTime LastAttemptedAt);

/// <summary>Response carrying all non-provisioned records (Pending or Failed) for the requested codes.</summary>
public record AccessCodesProvisioningStateResponse(IReadOnlyList<AccessCodeProvisioningStateItem> Items);
