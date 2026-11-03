namespace AlphaZero.Modules.Courses.Domain.Audits;

/// <summary>
/// The lifecycle event an enrollment can leave behind (issue #34 §3.1). The column is stored as
/// text on a generic audit table, so new kinds join without a schema change.
/// </summary>
public enum EnrollmentAuditEventKind
{
    /// <summary>Active → Suspended (enforced freeze, e.g. Payments revocation saga or admin).</summary>
    Suspended,

    /// <summary>Non-Active → Active. The Reactivate action (its domain event is "Resumed").</summary>
    Resumed,

    /// <summary>Active/Suspended → Expired (automated access-window expiry).</summary>
    Expired,

    /// <summary>
    /// Reserved for reconcile-style re-grants that are distinct from a plain user-visible
    /// reactivation. Not raised by the aggregate today.
    /// </summary>
    Reactivated,

    /// <summary>Any live state → Inactive (unenroll / Payments revocation surface).</summary>
    Deactivated,
}
