using System.Collections;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

public class Enrollment : TenantOwnedAggregate
{
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public Progress Progress { get; private set; } = null!;
    public DateTime EnrolledOn { get; private set; }

    /// <summary>
    /// Optional expiry instant (issue #34 stories #5/#6): staleness edge for enrollments whose
    /// originating course plan carries an access window. Null = never expires automatically.
    /// Frozen at enroll by intent ("they bought 30 days") — plan edits are future-only (§3.2.5).
    /// </summary>
    public DateTime? AccessExpiresOn { get; private set; }

    /// <summary>
    /// Set exactly once when a completion attempt flips the course to
    /// "all active items complete" (#32). Persists the completion transition so
    /// re-completions and progress recomputations after a section deletion never re-raise
    /// the completion event, even across process lifetimes.
    /// </summary>
    public DateTime? CompletedOn { get; private set; }

    private Enrollment()
    {
        // EF
    }

    private Enrollment(Guid id, Guid tenantId, Guid studentId, Guid courseId, DateTime? accessExpiresOn = null, Guid? coursePlanPrincipalId = null) : base(id, tenantId)
    {
        StudentId = studentId;
        CourseId = courseId;
        Status = EnrollmentStatus.Active;

        // Zero-length completion mask: new enrollments carry no persisted counts (ProgressCalculation
        // derives numerator/denominator from the course's ActiveItemsBitmask at calculation time)
        // and grow lazily as items are completed — adding curriculum items never touches enrollments.
        Progress = Progress.Create();
        EnrolledOn = DateTime.UtcNow;
        AccessExpiresOn = accessExpiresOn;
    }

    /// <summary>
    /// Creates an Active enrollment. <paramref name="accessExpiresOn"/> is derived from the
    /// originating course plan's access window by the enrolling flow (issue #34); plain
    /// self-serve enrollment without a plan passes null (never expires).
    /// <paramref name="coursePlanPrincipalId"/> rides the created event for the Identity grant
    /// branch (§3.2.2): the per-(course, plan) principal when enrolling under a windowed plan.
    /// </summary>
    public static ErrorOr<Enrollment> Create(Guid id, Guid tenantId, Guid studentId, Guid courseId, DateTime? accessExpiresOn = null, Guid? coursePlanPrincipalId = null)
    {
        if (studentId == Guid.Empty) return Error.Validation("Enrollment.StudentId", "StudentId is required.");
        if (courseId == Guid.Empty) return Error.Validation("Enrollment.CourseId", "CourseId is required.");

        var result =  new Enrollment(id, tenantId, studentId, courseId, accessExpiresOn, coursePlanPrincipalId);
        result.AddDomainEvent(new EnrollmentCreatedDomainEvent(id, studentId, courseId, tenantId, coursePlanPrincipalId));
        return result;
    }

    /// <param name="activeItemsBitmask">
    /// The current Course.ActiveItemsBitmask, loaded by the application handler. It bounds the valid
    /// bit-range, defines which positions belong to active items, and provides the percentage denominator.
    /// </param>
    public ErrorOr<Success> CompleteItem(int bitIndex, BitArray activeItemsBitmask)
    {
        if (Status != EnrollmentStatus.Active)
            return Error.Conflict("Enrollment.Status", "Cannot complete items in an inactive enrollment.");

        // Activity validation lives here, not in Progress (progress is just a varbit of completed
        // positions): only bits that map to an active curriculum item on the course can be completed.
        // Out-of-range is an invalid request (400); a cleared position is a state conflict (409).
        if (bitIndex < 0 || bitIndex >= activeItemsBitmask.Length)
            return Error.Validation("Progress.InvalidIndex", $"Bit index {bitIndex} is out of the course's allocated positions.");

        if (!activeItemsBitmask[bitIndex])
            return Error.Conflict("Progress.ItemInactive", "Only active curriculum items can be completed.");

        double oldPercentage = ProgressCalculation.CompletionPercentage(Progress.Bitmask, activeItemsBitmask);

        var result = Progress.MarkAsComplete(bitIndex);
        if (result.IsError) return result.Errors;

        Progress = result.Value;

        var newPercentage = ProgressCalculation.CompletionPercentage(Progress.Bitmask, activeItemsBitmask);
        if (oldPercentage != newPercentage)
        {
            AddDomainEvent(new ItemCompletedDomainEvent(Id, CourseId, bitIndex, oldPercentage, newPercentage));
        }

        // Completion milestone (#32): raised exactly when a completion attempt flips the
        // course to "all active items complete" over the course's current active-items mask
        // (#29). The persisted CompletedOn flag makes the transition idempotent — a second
        // raise is impossible, no matter how the bitmask is recomputed or re-driven afterwards.
        if (ProgressCalculation.IsAllComplete(Progress.Bitmask, activeItemsBitmask) && CompletedOn is null)
        {
            CompletedOn = DateTime.UtcNow;
            AddDomainEvent(new CourseCompletedDomainEvent(Id, CourseId, StudentId, TenantId, CompletedOn.Value));
        }

        return Result.Success;
    }

    // ------------------------------------------------------------------
    // Lifecycle transitions (issue #34) — the reachable states are exactly the
    // matrix pinned in the plan comment on the issue.
    // ------------------------------------------------------------------

    /// <summary>
    /// Active/Suspended/Expired → Inactive. Idempotent from Inactive (no rewrite).
    /// The deactivation path stays integration-event-less (the Payments revocation flow keeps its
    /// identity); it raises only the audit-only <see cref="EnrollmentDeactivatedDomainEvent"/> for
    /// the in-process audit writer (issue #34 §3.1).
    /// </summary>
    internal ErrorOr<Success> Deactivate()
    {
        if (Status is EnrollmentStatus.Inactive)
            return Result.Success; // idempotent (story #11 — duplicate-reaction safety for all transitions)

        if (Status is not (EnrollmentStatus.Active or EnrollmentStatus.Suspended or EnrollmentStatus.Expired))
            return InvalidTransition("Deactivate", Status, EnrollmentStatus.Inactive);

        var previousStatus = Status;
        ApplyTransition(EnrollmentStatus.Inactive);
        // Audit-only domain event (issue #34 §3.1): deactivation keeps its event-less integration
        // identity — no integration event is projected onto the bus for it (the Payments revocation
        // flow keeps publishing its own saga events). The in-process audit writer is the only
        // consumer, so the Inactive transition lands an audit row atomically with this mutation.
        //Domain Events Triggering is handled by the domain service now to ensure handling Operator Id and any other future metadata  

        //AddDomainEvent(new EnrollmentDeactivatedDomainEvent(Id, StudentId, CourseId, TenantId, previousStatus, operatorId));
        return Result.Success;
    }

    /// <summary>
    /// Active → Suspended. Idempotent from Suspended. Only reachable from Active — a revoked or
    /// expired enrollment has no access left to freeze; resolving it is Reactivate's job.
    /// </summary>
    internal ErrorOr<Success> Suspend(string? reason = null)
    {
        if (Status is EnrollmentStatus.Suspended)
            return Result.Success;

        if (Status is not EnrollmentStatus.Active)
            return InvalidTransition("Suspend", Status, EnrollmentStatus.Suspended);
        ApplyTransition(EnrollmentStatus.Suspended);
        return Result.Success;
    }

    /// <summary>
    /// Inactive/Expired/Suspended (and idempotently Active) → Active. Progress is untouched.
    /// </summary>
    internal ErrorOr<Success> Reactivate(Guid? operatorId = null)
    {
        if (Status is EnrollmentStatus.Active)
            return Result.Success;

        if (Status is not (EnrollmentStatus.Inactive or EnrollmentStatus.Suspended or EnrollmentStatus.Expired))
            return InvalidTransition("Reactivate", Status, EnrollmentStatus.Active);
        ApplyTransition(EnrollmentStatus.Active);
        return Result.Success;
    }

    /// <summary>
    /// Automated expiry (issues #34 stories #5/#6): Active/Suspended → Expired, idempotent from
    /// Expired. Not reachable from Inactive — nothing is on the clock once access is revoked.
    /// The aggregate owns the clock (same discipline as EnrolledOn/CompletedOn) — the sweep and
    /// the lazy-mint path both mean "expired at evaluation time".
    /// </summary>
    public ErrorOr<Success> MarkExpired()
    {
        if (Status is EnrollmentStatus.Expired)
            return Result.Success;

        if (Status is not (EnrollmentStatus.Active or EnrollmentStatus.Suspended))
            return InvalidTransition("Expire", Status, EnrollmentStatus.Expired);

        var previousStatus = Status;
        ApplyTransition(EnrollmentStatus.Expired);
        AddDomainEvent(new EnrollmentExpiredDomainEvent(Id, StudentId, CourseId, TenantId, previousStatus));
        return Result.Success;
    }

    private void ApplyTransition(EnrollmentStatus target)
    {
        // Transition metadata (who/when) lives in the enrollment audit trail (issue #34 §3.1),
        // carried by the domain events — the aggregate carries no read-boundary plumbing.
        Status = target;
    }

    private static Error InvalidTransition(string transition, EnrollmentStatus from, EnrollmentStatus to) =>
        Error.Conflict(
            "Enrollment.InvalidTransition",
            $"Enrollment is in state '{from}' and cannot be transitioned by '{transition}' to '{to}'.");
}