using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CourseSection : TenantOwnedEntity
{
    public string Title { get; private set; }
    public int Order { get; private set; }
    public IReadOnlyCollection<CourseSectionItem> Items => _items.AsReadOnly();
    private readonly List<CourseSectionItem> _items = new();

    private CourseSection(Guid id, Guid tenantId, string title, int order) : base(id, tenantId)
    {
        Title = title;
        Order = order;
    }

    internal static CourseSection Create(Guid tenantId, string title, int order)
    {
        return new CourseSection(Guid.NewGuid(), tenantId, title, order);
    }

    internal void AddItem(CourseSectionItem item)
    {
        _items.Add(item);
    }

    internal void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            _items.Remove(item);
            ReorderInternal();
        }
    }

    internal void ReorderInternal()
    {
        var sorted = _items.OrderBy(i => i.Order).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].UpdateOrder(i);
        }
    }

    internal void Update(string title, int order)
    {
        Title = title;
        Order = order;
    }
}


