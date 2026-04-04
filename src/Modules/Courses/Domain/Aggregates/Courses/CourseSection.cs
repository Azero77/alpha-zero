using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CourseSection : TenantOwnedEntity, ISoftDeletable
{
    public string Title { get; private set; }
    public int Order { get; private set; }
    public Guid CourseId { get; private set; }
    public IReadOnlyCollection<CourseSectionItem> Items => _items.AsReadOnly();
    private readonly List<CourseSectionItem> _items = new();
    public bool IsDeleted { get; private set; }

    public DateTime? OnDeleted { get; private set; } = null;

    private CourseSection(Guid id, Guid tenantId, string title, int order, Guid courseId) : base(id, tenantId)
    {
        Title = title;
        Order = order;
        CourseId = courseId;
    }

    internal static CourseSection Create(Guid tenantId, string title, int order,Guid courseId)
    {
        return new CourseSection(Guid.NewGuid(), tenantId, title, order, courseId);
    }

    internal void AddItem(CourseSectionItem item)
    {
        _items.Add(item);
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
        return Result.Success;
    }

    internal ErrorOr<Success> Restore()
    {
        if (!IsDeleted)
            return Error.Failure("Section.Failure", "Section is not deleted.");

        IsDeleted = false;
        return Result.Success;
    }
}


