using AlphaZero.Shared.Authorization;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Enrollments;

/// <summary>
/// Resolves the acting principal for lifecycle transitions (issue #34 story #10): admin/support
/// transitions carry the authorized actor id; automation paths (bus consumers, expiry scan) have
/// no principal and stay <c>null</c> — the audit will read "system", not a fabricated actor.
/// </summary>
public static class EnrollmentTransitionActor
{
    public const string RevocationSagaServiceName = "Payments.RevocationSaga";

    /// <summary>
    /// Story #10 gate (§3.5): deactivation/suspension/reactivation may be fired by exactly two
    /// kinds of actor, verified BEFORE the aggregate is loaded:
    /// 1. the Payments revocation saga — authorized IAM machine identity (<see cref="SystemServiceAuthorizationContext"/>);
    /// 2. a tenant-admin actor arriving through IAM (either an explicit Principal or evaluated at the AccessControl'd edge).
    /// Everything else — rogue in-process sends with no context or unauthorized services — fails closed.
    /// </summary>
    public static ErrorOr<Success> EnsureCanFire(AuthorizationContext? context)
    {
        if (context is null)
        {
            return Error.Forbidden(
                "Enrollment.TransitionUnauthorized",
                "Enrollment lifecycle transitions can only be fired by the payments revocation saga or a tenant-admin actor.");
        }
        // 1. Authorized System / Machine identity (Payments revocation saga)
        if (context is SystemServiceAuthorizationContext systemCtx)
        {
            if (string.Equals(systemCtx.ServiceName, RevocationSagaServiceName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(systemCtx.RequiredPermission, EnrollmentPermissions.Deactivate, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success;
            }

            return Error.Forbidden(
                "Enrollment.TransitionUnauthorized",
                $"System service '{systemCtx.ServiceName}' with permission '{systemCtx.RequiredPermission}' is not authorized to fire enrollment transitions.");
        }

        // 2. Request that passed an AccessControl endpoint evaluated at the edge
        var accessControlled = !string.IsNullOrWhiteSpace(context.RequiredPermission)
            && !string.IsNullOrWhiteSpace(context.ResourcePath);

        if (accessControlled)
        {
            return Result.Success;
        }

        return Error.Forbidden(
            "Enrollment.TransitionUnauthorized",
            "Enrollment lifecycle transitions require a valid tenant-admin actor or authorized system service.");
    }
}
