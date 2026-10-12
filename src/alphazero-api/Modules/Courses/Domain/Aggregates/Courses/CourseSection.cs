using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CourseSection : Entity, ISoftDeletable
{
    public string Title { get; private set; }
    public int Order { get; private set; }
    public Guid CourseId { get; private set; }
    public IReadOnlyCollection<CurriculumItem> Items => _items.AsReadOnly();
    private readonly List<CurriculumItem> _items = new();
    public bool IsDeleted { get; private set; }

    public DateTime? OnDeleted { get; private set; } = null;

    private CourseSection(Guid id, string title, int order, Guid courseId, List<CurriculumItem>? items) : base(id)
    {
        Title = title;
        Order = order;
        CourseId = courseId;
        _items = items;
    }

    internal static ErrorOr<CourseSection> Create(Guid id, string title, int order, Guid courseId,
        List<CurriculumItem>? items = null)
    {
        if (string.IsNullOrEmpty(title))
            return Error.Validation("","");
        return new CourseSection(id, title, order, courseId, items);
    }

    public void AddItem(CurriculumItem item)
    {
        item.Order = _items.Count;
        _items.Add(item);
    }

    public ErrorOr<CurriculumItem> AddItem(
        Guid id,
        string title,
        ResourceType mainType,
        int bitIndex,
        IEnumerable<CurriculumResource>? resources = null)
    {
        var itemResult = CurriculumItem.Create(id, Id, title, _items.Count, bitIndex, mainType, resources);
        if (itemResult.IsError)
            return itemResult.Errors;

        _items.Add(itemResult.Value);
        return itemResult.Value;
    }

    public ErrorOr<CurriculumItem> UpdateItem(
        Guid itemId,
        string title,
        ResourceType mainType,
        IEnumerable<CurriculumResource>? resources = null)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId && !i.IsDeleted);
        if (item is null)
            return Error.NotFound("CurriculumItem.NotFound", "Item not found in section.");

        var updateResult = item.Update(title, mainType, resources);
        if (updateResult.IsError)
            return updateResult.Errors;

        return item;
    }

    public ErrorOr<CurriculumItem> RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId && !i.IsDeleted);
        if (item is null)
            return Error.NotFound("CurriculumItem.NotFound", "Item not found in section.");

        var delResult = item.Delete();
        if (delResult.IsError)
            return delResult.Errors;

        return item;
    }

    public CurriculumItem? GetItem(Guid itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId && !i.IsDeleted);
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

    internal ErrorOr<Success> Delete()
    {
        if (IsDeleted)
            return Error.Failure("Section.Failure", "Section is already deleted.");
        
        IsDeleted = true;
        OnDeleted = DateTime.UtcNow;
        return Result.Success;
    }

    internal ErrorOr<Success> Restore()
    {
        if (!IsDeleted)
            return Error.Failure("Section.Failure", "Section is not deleted.");

        IsDeleted = false;
        OnDeleted = null;
        return Result.Success;
    }
}
