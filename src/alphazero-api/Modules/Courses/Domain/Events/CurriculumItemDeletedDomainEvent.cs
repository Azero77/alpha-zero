using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// A curriculum item was deleted. The BitIndex is the value captured BEFORE the item was
/// soft-deleted/removed from its section — deleting never shifts other indices and the
/// position is never reused (NextAvailableBitIndex only grows).
/// </summary>
public class CurriculumItemDeletedDomainEvent : DomainEvent
{
    public Guid CourseId { get; }
    public Guid SectionId { get; }
    public Guid ItemId { get; }
    public int BitIndex { get; }

    public CurriculumItemDeletedDomainEvent(Guid courseId, Guid sectionId, Guid itemId, int bitIndex)
    {
        CourseId = courseId;
        SectionId = sectionId;
        ItemId = itemId;
        BitIndex = bitIndex;
    }
}
