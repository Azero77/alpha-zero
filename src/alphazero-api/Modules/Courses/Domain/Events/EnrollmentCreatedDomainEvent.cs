using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollmentCreatedDomainEvent : DomainEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    public EnrollmentCreatedDomainEvent(Guid enrollmentId, Guid studentId, Guid courseId, Guid tenantId = default)
    {
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
    }
}
