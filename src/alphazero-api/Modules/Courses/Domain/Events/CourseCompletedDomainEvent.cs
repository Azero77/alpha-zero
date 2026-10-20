using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// Raised exactly once per enrollment: when a completion attempt flips the course to
/// "all active items complete" (<c>ProgressCalculation.IsAllComplete</c> over the
/// course's active-items mask). Re-completions and
/// progress recomputations after a section deletion never re-raise it — the persisted
/// <c>CompletedOn</c> flag on the enrollment guards the transition (#32).
/// </summary>
public class CourseCompletedDomainEvent : DomainEvent
{
    public Guid EnrollmentId { get; }
    public Guid CourseId { get; }
    public Guid StudentId { get; }
    public Guid TenantId { get; }
    public DateTime CompletedOnUtc { get; }

    public CourseCompletedDomainEvent(Guid enrollmentId, Guid courseId, Guid studentId, Guid tenantId, DateTime completedOnUtc)
    {
        EnrollmentId = enrollmentId;
        CourseId = courseId;
        StudentId = studentId;
        TenantId = tenantId;
        CompletedOnUtc = completedOnUtc;
    }
}
