using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollmentResumedDomainEvent : DomainEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    public EnrollmentResumedDomainEvent(Guid enrollmentId, Guid studentId, Guid courseId, Guid tenantId)
    {
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
    }
}
