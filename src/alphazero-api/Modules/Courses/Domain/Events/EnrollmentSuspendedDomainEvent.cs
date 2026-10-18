using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollmentSuspendedDomainEvent : DomainEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }
    public string? Reason { get; }

    public EnrollmentSuspendedDomainEvent(Guid enrollmentId, Guid studentId, Guid courseId, Guid tenantId, string? reason = null)
    {
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
        Reason = reason;
    }
}
