using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// A curriculum item was added. The allocated BitIndex is carried explicitly — the active-items
/// handler activates exactly this position on Course.ActiveItemsBitmask.
/// </summary>
public class CurriculumItemAddedDomainEvent : DomainEvent
{
    public Guid CourseId { get; }
    public Guid SectionId { get; }
    public Guid ItemId { get; }
    public int BitIndex { get; }

    public CurriculumItemAddedDomainEvent(Guid courseId, Guid sectionId, Guid itemId, int bitIndex)
    {
        CourseId = courseId;
        SectionId = sectionId;
        ItemId = itemId;
        BitIndex = bitIndex;
    }
}
