namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Request-response contract (issue #59): Library asks Courses for the provisioning state of
/// its activated access codes. Courses answers with the non-Provisioned records that exist in
/// its FailedProvisioningRecords table; codes without a record are simply absent from the
/// response (Library treats them as having no failed provisioning entry).
/// Handled asynchronously via MassTransit request-response over the module bus.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AccessCodeIds"/> MUST be non-empty. There is intentionally NO tenant-wide fallback
/// for an empty list: Library always supplies the concrete activated-code ids (empty means
/// "nothing activated, nothing to reconcile" — Library skips the request entirely). A consumer
/// that receives an empty list answers with an empty response rather than the tenant's full
/// record set, so no accidental cross-module data fan-out is possible.
/// </para>
/// </remarks>
public record GetAccessCodesProvisioningStateRequest(Guid TenantId, IReadOnlyList<Guid> AccessCodeIds);

/// <summary>
/// State of one non-provisioned access code in Courses. <paramref name="Status"/> is the string
/// form of the Courses-side <c>ProvisioningStatus</c> enum; over this contract only the values
/// <c>"Pending"</c> and <c>"Failed"</c> are produced (the consumer filters out
/// <c>Provisioned</c>). Consumers should treat unknown future values like Failed.
/// </summary>
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
