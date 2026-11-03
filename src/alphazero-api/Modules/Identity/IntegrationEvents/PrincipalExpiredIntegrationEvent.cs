namespace AlphaZero.Modules.Identity.IntegrationEvents;

/// <summary>
/// Identity's FIRST cross-module integration event (issue #34 rework §3.4 path a, steps 7/8):
/// raised when the <c>AccessTicketValid</c> condition operator sees an access ticket whose
/// course-entitlement WINDOW has elapsed. Courses consumes it to run the enrollment aggregate's
/// idempotent guarded <c>MarkExpired</c> — the lazy, request-driven sibling of the scheduled
/// sweep. Best-effort publish + consumed-side idempotency absorb duplicate evaluations.
///
/// Payload (owner-approved tuple, §2 R2-Q5): <c>(PrincipalId, UserId, TenantId, ResourcePath)</c>.
/// <see cref="UserId"/> is the acting identity of the <c>AuthorizationContext</c> (the tenant
/// user whose enrollments the consumer resolves together with the course inside
/// <see cref="ResourcePath"/> — tomb ARN `az:{Service}:{TenantId}:{ResourcePath}`). For
/// principal-scoped actors (support/global) <see cref="PrincipalId"/> is set; the consumer
/// no-ops when no matching enrollment exists.
/// Wire identity: natural type-name URN per #31's final decision (no MessageUrn pin).
/// </summary>
public record PrincipalExpiredIntegrationEvent(
    Guid? PrincipalId,
    Guid UserId,
    Guid TenantId,
    string ResourcePath);
