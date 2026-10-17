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
    /// Last-transition audit metadata (issue #34 stories #9): who fired the transition and when.
    /// Null for enrollments that have never left Active, and null <see cref="LastStatusChangedBy"/>
    /// for automated (saga / expiry-scan) transitions that have no acting principal.
    /// </summary>
    public DateTime? LastStatusChangedOn { get; private set; }
    public Guid? LastStatusChangedBy { get; private set; }

    private Enrollment()
    {
        // EF
    }

    private Enrollment(Guid id, Guid tenantId, Guid studentId, Guid courseId, int totalTrackedItems, int? activeTrackedItems = null) : base(id, tenantId)
    {
        StudentId = studentId;
        CourseId = courseId;
        Status = EnrollmentStatus.Active;
        Progress = Progress.Create(totalTrackedItems, activeTrackedItems ?? totalTrackedItems);
        EnrolledOn = DateTime.UtcNow;

        AddDomainEvent(new EnrollmentCreatedDomainEvent(Id, StudentId, CourseId, TenantId));
    }

    public static ErrorOr<Enrollment> Create(Guid id, Guid tenantId, Guid studentId, Guid courseId, int totalTrackedItems, int? activeTrackedItems = null)
    {
        if (studentId == Guid.Empty) return Error.Validation("Enrollment.StudentId", "StudentId is required.");
        if (courseId == Guid.Empty) return Error.Validation("Enrollment.CourseId", "CourseId is required.");
        if (totalTrackedItems < 0) return Error.Validation("Enrollment.Progress", "Total tracked items cannot be negative.");
        if (activeTrackedItems.HasValue && activeTrackedItems.Value < 0) return Error.Validation("Enrollment.Progress", "Active tracked items cannot be negative.");

        return new Enrollment(id, tenantId, studentId, courseId, totalTrackedItems, activeTrackedItems);
    }

    public ErrorOr<Success> CompleteItem(int bitIndex)
    {
        if (Status != EnrollmentStatus.Active) 
            return Error.Conflict("Enrollment.Status", "Cannot complete items in an inactive enrollment.");

        double oldPercentage = Progress.CompletionPercentage;

        var result = Progress.MarkAsComplete(bitIndex);
        if (result.IsError) return result.Errors;

        Progress = result.Value;

        if (oldPercentage != Progress.CompletionPercentage)
        {
            AddDomainEvent(new ItemCompletedDomainEvent(Id, CourseId, bitIndex, oldPercentage, Progress.CompletionPercentage));
        }

        return Result.Success;
    }

    // ------------------------------------------------------------------
    // Lifecycle transitions (issue #34) — the reachable states are exactly the
    // matrix pinned in the plan comment on the issue.
    // ------------------------------------------------------------------

    /// <summary>
    /// Active/Suspended/Expired → Inactive. Idempotent from Inactive (no rewrite).
    /// The deactivation path stays event-less: the Payments revocation flow keeps its identity.
    /// </summary>
    public ErrorOr<Success> Deactivate(Guid? operatorId = null)
    {
        if (Status is EnrollmentStatus.Inactive)
            return Result.Success; // idempotent (story #11 — duplicate-reaction safety for all transitions)

        if (Status is not (EnrollmentStatus.Active or EnrollmentStatus.Suspended or EnrollmentStatus.Expired))
            return InvalidTransition("Deactivate", Status, EnrollmentStatus.Inactive);

        ApplyTransition(EnrollmentStatus.Inactive, operatorId);
        return Result.Success;
    }

    /// <summary>
    /// Active → Suspended. Idempotent from Suspended. Only reachable from Active — a revoked or
    /// expired enrollment has no access left to freeze; resolving it is Reactivate's job.
    /// </summary>
    public ErrorOr<Success> Suspend(Guid? operatorId = null, string? reason = null)
    {
        if (Status is EnrollmentStatus.Suspended)
            return Result.Success;

        if (Status is not EnrollmentStatus.Active)
            return InvalidTransition("Suspend", Status, EnrollmentStatus.Suspended);

        ApplyTransition(EnrollmentStatus.Suspended, operatorId);
        AddDomainEvent(new EnrollmentSuspendedDomainEvent(Id, StudentId, CourseId, TenantId, reason));
        return Result.Success;
    }

    /// <summary>
    /// Inactive/Expired/Suspended (and idempotently Active) → Active. Progress is untouched.
    /// </summary>
    public ErrorOr<Success> Reactivate(Guid? operatorId = null)
    {
        if (Status is EnrollmentStatus.Active)
            return Result.Success;

        if (Status is not (EnrollmentStatus.Inactive or EnrollmentStatus.Suspended or EnrollmentStatus.Expired))
            return InvalidTransition("Reactivate", Status, EnrollmentStatus.Active);

        ApplyTransition(EnrollmentStatus.Active, operatorId);
        AddDomainEvent(new EnrollmentResumedDomainEvent(Id, StudentId, CourseId, TenantId));
        return Result.Success;
    }

    /// <summary>
    /// Automated expiry (issues #34 stories #5/#6): Active/Suspended → Expired, idempotent from
    /// Expired. Not reachable from Inactive — nothing is on the clock once access is revoked.
    /// </summary>
    public ErrorOr<Success> MarkExpired(DateTime occuredOn)
    {
        if (Status is EnrollmentStatus.Expired)
            return Result.Success;

        if (Status is not (EnrollmentStatus.Active or EnrollmentStatus.Suspended))
            return InvalidTransition("Expire", Status, EnrollmentStatus.Expired);

        ApplyTransition(EnrollmentStatus.Expired, operatorId: null, changedOn: occuredOn);
        AddDomainEvent(new EnrollmentExpiredDomainEvent(Id, StudentId, CourseId, TenantId));
        return Result.Success;
    }

    private void ApplyTransition(EnrollmentStatus target, Guid? operatorId, DateTime? changedOn = null)
    {
        Status = target;
        LastStatusChangedOn = changedOn ?? DateTime.UtcNow;
        LastStatusChangedBy = operatorId;
    }

    private static Error InvalidTransition(string transition, EnrollmentStatus from, EnrollmentStatus to) =>
        Error.Conflict(
            "Enrollment.InvalidTransition",
            $"Enrollment is in state '{from}' and cannot be transitioned by '{transition}' to '{to}'.");
}
