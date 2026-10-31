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
    public static Guid? Resolve(IAuthorizationContextProvider provider)
    {
        try
        {
            var contextResult = provider.Get();
            return contextResult.IsError ? null : contextResult.Value?.Id;
        }
        catch
        {
            // contexts outside a request (bus consumers, background scans) have no actor
            return null;
        }
    }

    /// <summary>
    /// Story #10 gate (§3.5): deactivation/suspension/reactivation may be fired by exactly two
    /// kinds of actor, verified BEFORE the aggregate is loaded:
    /// 1. the Payments revocation saga — bus automation, which claims its transport origin
    ///    explicitly (<see cref="EnrollmentTransitionAutomation"/>); and
    /// 2. a tenant-admin/syt actor arriving through the AccessControl envelope (the FastEndpoints
    ///    edge attached the resource permission and IAM evaluated it).
    /// Everything else — rogue in-process sends with no HTTP context, or authenticated calls that
    /// never passed an AccessControl'd endpoint — fails closed before any state transition can
    /// happen. The error surfaces as 409/403 at the HTTP boundary (behavior pipeline).
    /// </summary>
    public static ErrorOr<Success> EnsureCanFire(AuthorizationContext? context)
    {
        if (context is null)
        {
            if (EnrollmentTransitionAutomation.HasTransportClaim())
            {
                return Result.Success; // transport-claimed automation (revocation saga)
            }

            return Error.Forbidden(
                "Enrollment.TransitionUnauthorized",
                "Enrollment lifecycle transitions can only be fired by the payments revocation saga or a tenant-admin actor.");
        }

        var accessControlled = !string.IsNullOrWhiteSpace(context.RequiredPermission)
            && !string.IsNullOrWhiteSpace(context.ResourcePath);

        if (accessControlled)
        {
            return Result.Success; // tenant-admin/syt: evaluated at the AccessControl'd edge
        }

        return Error.Forbidden(
            "Enrollment.TransitionUnauthorized",
            "Enrollment lifecycle transitions require the AccessControl envelope (an authenticated call that did not pass an access-controlled endpoint may not fire them).");
    }
}
