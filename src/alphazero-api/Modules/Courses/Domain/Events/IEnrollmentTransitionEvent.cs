using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// Contract over the enrollment TRANSITION events (issue #34 §3.1): everything the audit writer
/// needs to record the change without touching the aggregate or the read-path plumbing.
/// </summary>
public interface IEnrollmentTransitionEvent
{
    Guid EnrollmentId { get; }
    Guid TenantId { get; }

    /// <summary>The status the enrollment left (audit Metadata "From").</summary>
    EnrollmentStatus FromStatus { get; }

    /// <summary>The acting principal, or null for automated transitions (audit ActorId).</summary>
    Guid? ActorId { get; }
}
