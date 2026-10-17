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

    public void Deactivate() => Status = EnrollmentStatus.Inactive;
    public void Activate() => Status = EnrollmentStatus.Active;

    // Instructor-driven curriculum edits (e.g. a section soft-delete) change the number of
    // active tracked items; the earned bitmask stays stable and only the active denominator
    // is refilled, so the completion percentage stays fair (see BitmaskStabilityTests).
    public void UpdateActiveItems(int activeItems)
    {
        Progress = Progress.WithActiveItems(activeItems);
    }
}
