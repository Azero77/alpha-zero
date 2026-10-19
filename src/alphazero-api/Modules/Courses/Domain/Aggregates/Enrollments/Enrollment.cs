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

    private Enrollment()
    {
        // EF
    }

    private Enrollment(Guid id, Guid tenantId, Guid studentId, Guid courseId) : base(id, tenantId)
    {
        StudentId = studentId;
        CourseId = courseId;
        Status = EnrollmentStatus.Active;

        // Zero-length completion mask: new enrollments carry no persisted counts (ProgressCalculation
        // derives numerator/denominator from the course's ActiveItemsBitmask at calculation time)
        // and grow lazily as items are completed — adding curriculum items never touches enrollments.
        Progress = Progress.Create();
        EnrolledOn = DateTime.UtcNow;

        AddDomainEvent(new EnrollmentCreatedDomainEvent(Id, StudentId, CourseId, TenantId));
    }

    public static ErrorOr<Enrollment> Create(Guid id, Guid tenantId, Guid studentId, Guid courseId)
    {
        if (studentId == Guid.Empty) return Error.Validation("Enrollment.StudentId", "StudentId is required.");
        if (courseId == Guid.Empty) return Error.Validation("Enrollment.CourseId", "CourseId is required.");

        return new Enrollment(id, tenantId, studentId, courseId);
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
        if (bitIndex < 0 || bitIndex >= activeItemsBitmask.Length || !activeItemsBitmask[bitIndex])
            return Error.Validation("Progress.ItemInactive", "Only active curriculum items can be completed.");

        double oldPercentage = ProgressCalculation.CompletionPercentage(Progress.Bitmask, activeItemsBitmask);

        var result = Progress.MarkAsComplete(bitIndex);
        if (result.IsError) return result.Errors;

        Progress = result.Value;

        if (oldPercentage != ProgressCalculation.CompletionPercentage(Progress.Bitmask, activeItemsBitmask))
        {
            var newPercentage = ProgressCalculation.CompletionPercentage(Progress.Bitmask, activeItemsBitmask);
            AddDomainEvent(new ItemCompletedDomainEvent(Id, CourseId, bitIndex, oldPercentage, newPercentage));
        }

        return Result.Success;
    }

    public void Deactivate() => Status = EnrollmentStatus.Inactive;
    public void Activate() => Status = EnrollmentStatus.Active;
}
