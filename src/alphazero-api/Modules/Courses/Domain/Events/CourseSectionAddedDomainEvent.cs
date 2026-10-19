using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// A section was added to the course. Sections are containers, not progress-tracked items:
/// the active-items handler treats this event as a no-op unless items are attached.
/// </summary>
public class CourseSectionAddedDomainEvent : DomainEvent
{
    public Guid CourseId { get; }
    public Guid SectionId { get; }

    public CourseSectionAddedDomainEvent(Guid courseId, Guid sectionId)
    {
        CourseId = courseId;
        SectionId = sectionId;
    }
}
