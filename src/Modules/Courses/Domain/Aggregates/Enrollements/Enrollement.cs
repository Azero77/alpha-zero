using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;

public class Enrollement : TenantOwnedAggregate
{
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public EnrollementStatus Status { get; private set; }
    public Progress Progress { get; private set; }
    public DateTime EnrolledOn { get; private set; }

    private Enrollement(Guid id, Guid tenantId, Guid studentId, Guid courseId, int totalTrackedItems) : base(id, tenantId)
    {
        StudentId = studentId;
        CourseId = courseId;
        Status = EnrollementStatus.Active;
        Progress = Progress.Create(totalTrackedItems);
        EnrolledOn = DateTime.UtcNow;

        AddDomainEvent(new EnrollementCreatedDomainEvent(Id, StudentId, CourseId));
    }

    public static ErrorOr<Enrollement> Create(Guid id, Guid tenantId, Guid studentId, Guid courseId, int totalTrackedItems)
    {
        if (studentId == Guid.Empty) return Error.Validation("Enrollement.StudentId", "StudentId is required.");
        if (courseId == Guid.Empty) return Error.Validation("Enrollement.CourseId", "CourseId is required.");
        if (totalTrackedItems < 0) return Error.Validation("Enrollement.Progress", "Total tracked items cannot be negative.");

        return new Enrollement(id, tenantId, studentId, courseId, totalTrackedItems);
    }

    public ErrorOr<Success> CompleteItem(int bitIndex)
    {
        if (Status != EnrollementStatus.Active) 
            return Error.Conflict("Enrollement.Status", "Cannot complete items in an inactive enrollment.");

        return Progress.MarkAsComplete(bitIndex);
    }

    public void Deactivate() => Status = EnrollementStatus.Inactive;
    public void Activate() => Status = EnrollementStatus.Active;
}
