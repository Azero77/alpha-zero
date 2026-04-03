using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public abstract class CourseSectionItem : TenantOwnedEntity
{
    public Guid ResourceId { get; private set; }
    public int Order { get; internal set; } // UI Display Order
    public int BitIndex { get; private set; } // Immutable Bitmask Pointer
    public string Title { get; private set; }

    protected CourseSectionItem(Guid id, Guid tenantId, string title, Guid resourceId, int order, int bitIndex) : base(id, tenantId)
    {
        Title = title;
        ResourceId = resourceId;
        Order = order;
        BitIndex = bitIndex;
    }

    internal void UpdateOrder(int newOrder) => Order = newOrder;
}

public class CourseSectionLesson : CourseSectionItem
{
    internal CourseSectionLesson(Guid id, Guid tenantId, string title, Guid videoId, int order, int bitIndex) 
        : base(id, tenantId, title, videoId, order, bitIndex)
    {
    }

    public Guid VideoId => ResourceId;
}

public class CourseSectionQuiz : CourseSectionItem
{
    internal CourseSectionQuiz(Guid id, Guid tenantId, string title, Guid quizId, int order, int bitIndex) 
        : base(id, tenantId, title, quizId, order, bitIndex)
    {
    }

    public Guid QuizId => ResourceId;
}

public class CourseSectionDocument : CourseSectionItem
{
    internal CourseSectionDocument(Guid id, Guid tenantId, string title, Guid documentId, int order, int bitIndex) 
        : base(id, tenantId, title, documentId, order, bitIndex)
    {
    }

    public Guid DocumentId => ResourceId;
}


