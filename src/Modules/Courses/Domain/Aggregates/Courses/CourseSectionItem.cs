using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public abstract class CourseSectionItem : TenantOwnedEntity, ISoftDeletable
{
    public Guid ResourceId { get; private set; }
    public Guid SectionId { get; private set; }
    public int Order { get; internal set; } // UI Display Order
    public int BitIndex { get; private set; } // Immutable Bitmask Pointer
    public string Title { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? OnDeleted { get; private set; } = null!;
    public JsonElement Metadata { get; private set; }

    protected CourseSectionItem(Guid id, Guid tenantId, string title, Guid resourceId, int order, int bitIndex, JsonElement metadata) : base(id, tenantId)
    {
        Title = title;
        ResourceId = resourceId;
        Order = order;
        BitIndex = bitIndex;
        Metadata = metadata;
    }

    internal void UpdateOrder(int newOrder) => Order = newOrder;
    internal void UpdateResource(Guid resourceId) => ResourceId = resourceId;
    internal void SetMetadata(JsonElement metadata) => Metadata = metadata;
}

public class CourseSectionLesson : CourseSectionItem
{
    internal CourseSectionLesson(Guid id, Guid tenantId, string title, Guid resourceId, int order, int bitIndex, JsonElement metadata) 
        : base(id, tenantId, title, resourceId, order, bitIndex, metadata)
    {
    }

    public Guid VideoId => ResourceId;
}

public class CourseSectionAssessment : CourseSectionItem
{
    internal CourseSectionAssessment(Guid id, Guid tenantId, string title, Guid resourceId, int order, int bitIndex, JsonElement metadata) 
        : base(id, tenantId, title, resourceId, order, bitIndex, metadata)
    {
    }

    public Guid AssessmentId => ResourceId;
}

public class CourseSectionDocument : CourseSectionItem
{
    internal CourseSectionDocument(Guid id, Guid tenantId, string title, Guid resourceId, int order, int bitIndex, JsonElement metadata) 
        : base(id, tenantId, title, resourceId, order, bitIndex, metadata)
    {
    }
    public Guid DocumentId => ResourceId;
}
