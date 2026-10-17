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

        // Completion milestone (#32): raised exactly when a completion attempt flips the
        // course to "all active items complete" (Progress.IsAllComplete). The persisted
        // CompletedOn flag makes the transition idempotent — a second raise is impossible,
        // no matter how the bitmask is recomputed or re-driven afterwards.
        if (Progress.IsAllComplete && CompletedOn is null)
        {
            CompletedOn = DateTime.UtcNow;
            AddDomainEvent(new CourseCompletedDomainEvent(Id, CourseId, StudentId, TenantId, CompletedOn.Value));
        }

        return Result.Success;
    }

    public void Deactivate() => Status = EnrollmentStatus.Inactive;
    public void Activate() => Status = EnrollmentStatus.Active;
}