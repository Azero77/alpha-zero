using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollmentExpiredDomainEvent : DomainEvent, IEnrollmentTransitionEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    /// <summary>The status the enrollment left (audit Metadata "From", issue #34 §3.1).</summary>
    public EnrollmentStatus FromStatus { get; }

    /// <summary>
    /// Expiry is always automated; the aggregate's guard pins the actor to null (the audit row
    /// reads "system"). The field exists so the audit writer stays uniform across event kinds.
    /// </summary>
    public Guid? ActorId => null;

    public EnrollmentExpiredDomainEvent(
        Guid enrollmentId, Guid studentId, Guid courseId, Guid tenantId, EnrollmentStatus fromStatus)
    {
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
        FromStatus = fromStatus;
    }
}
