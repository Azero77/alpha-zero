using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// A section was deleted. The payload captures the BitIndex values of every active curriculum
/// item the section contained BEFORE the resources were removed from the in-memory aggregate,
/// so the active-items handler never has to reconstruct deleted state.
/// </summary>
public class CourseSectionDeletedDomainEvent : DomainEvent
{
    public Guid CourseId { get; }
    public Guid SectionId { get; }
    public IReadOnlyList<int> BitIndexes { get; }

    public CourseSectionDeletedDomainEvent(Guid courseId, Guid sectionId, IReadOnlyList<int> bitIndexes)
    {
        CourseId = courseId;
        SectionId = sectionId;
        BitIndexes = bitIndexes.ToList().AsReadOnly();
    }
}
