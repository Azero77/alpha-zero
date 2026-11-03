using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollmentResumedDomainEvent : DomainEvent, IEnrollmentTransitionEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    /// <summary>The status the enrollment left (audit Metadata "From", issue #34 §3.1).</summary>
    public EnrollmentStatus FromStatus { get; }

    /// <summary>The acting principal, or null for automated (saga) reactivations.</summary>
    public Guid? ActorId { get; }

    public EnrollmentResumedDomainEvent(
        Guid enrollmentId, Guid studentId, Guid courseId, Guid tenantId,
        EnrollmentStatus fromStatus, Guid? actorId)
    {
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
        FromStatus = fromStatus;
        ActorId = actorId;
    }
}
