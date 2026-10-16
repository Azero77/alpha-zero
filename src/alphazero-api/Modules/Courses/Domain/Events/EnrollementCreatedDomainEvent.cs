using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollementCreatedDomainEvent : DomainEvent
{
    public Guid EnrollementId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    public EnrollementCreatedDomainEvent(Guid enrollementId, Guid studentId, Guid courseId, Guid tenantId = default)
    {
        EnrollementId = enrollementId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
    }
}
