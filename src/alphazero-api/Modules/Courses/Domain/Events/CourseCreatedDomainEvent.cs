using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class CourseCreatedDomainEvent : DomainEvent
{
    public Guid CourseId { get; }
    public Guid TenantId { get; }

    public CourseCreatedDomainEvent(Guid courseId, Guid tenantId)
    {
        CourseId = courseId;
        TenantId = tenantId;
    }
}
