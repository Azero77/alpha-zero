using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class CoursePublishedDomainEvent : DomainEvent
{
    public Guid CourseId { get; }

    public CoursePublishedDomainEvent(Guid courseId)
    {
        CourseId = courseId;
    }
}
