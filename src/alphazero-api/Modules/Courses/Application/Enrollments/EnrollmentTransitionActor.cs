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
}
