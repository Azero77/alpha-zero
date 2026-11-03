using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// Domain-ONLY deactivation event (issue #34 §3.1): it exists exclusively so the in-process audit
/// writer can record the → Inactive transition atomically with the aggregate mutation. It is
/// deliberately NOT projected onto the module bus — deactivation keeps its event-less integration
/// identity (the Payments revocation flow keeps publishing its own saga events).
/// </summary>
public class EnrollmentDeactivatedDomainEvent : DomainEvent, IEnrollmentTransitionEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    /// <summary>The status the enrollment left (audit Metadata "From", issue #34 §3.1).</summary>
    public EnrollmentStatus FromStatus { get; }

    /// <summary>The acting principal, or null for automated (revocation-saga) deactivations.</summary>
    public Guid? ActorId { get; }

    public EnrollmentDeactivatedDomainEvent(
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
