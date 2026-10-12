using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CurriculumItem : Entity, ISoftDeletable
{
    public string Title { get; private set; }
    public Guid SectionId { get; private set; }
    public int Order { get; internal set; } // UI Display Order
    public int BitIndex { get; private set; } // Immutable Bitmask Pointer
    public ResourceType MainResourceType { get; private set; } // "Video", "Document", "Article", "Quiz"
    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; } = null;

    private readonly List<CurriculumResource> _resources = new();
    public IReadOnlyCollection<CurriculumResource> Resources => _resources.AsReadOnly();

    private CurriculumItem()
    {
    } // EF Core

    public CurriculumItem(
        Guid id,
        Guid sectionId,
        string title,
        int order,
        int bitIndex,
        ResourceType mainType,
        IEnumerable<CurriculumResource>? resources = null)
        : base(id)
    {
        SectionId = sectionId;
        Title = title;
        Order = order;
        BitIndex = bitIndex;
        MainResourceType = mainType;
        if (resources != null)
        {
            SetResources(resources);
        }
    }

    public static ErrorOr<CurriculumItem> Create(
        Guid id,
        Guid sectionId,
        string title,
        int order,
        int bitIndex,
        ResourceType mainType,
        IEnumerable<CurriculumResource>? resources = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("CurriculumItem.Title", "Title is required.");

        return new CurriculumItem(id, sectionId, title, order, bitIndex, mainType, resources);
    }

    public ErrorOr<Success> Update(string title, ResourceType mainType, IEnumerable<CurriculumResource>? resources = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("CurriculumItem.Title", "Title is required.");

        Title = title;
        MainResourceType = mainType;

        if (resources != null)
        {
            SetResources(resources);
        }

        return Result.Success;
    }

    public void AddResource(CurriculumResource resource)
    {
        resource.CurriculumItemId = Id;
        resource.UpdateOrder(_resources.Count);
        _resources.Add(resource);
    }

    public void RemoveResource(Guid resourceId)
    {
        var resource = _resources.FirstOrDefault(r => r.Id == resourceId);
        if (resource != null)
        {
            _resources.Remove(resource);
            for (int i = 0; i < _resources.Count; i++)
            {
                _resources[i].UpdateOrder(i);
            }
        }
    }

    public void ClearResources() => _resources.Clear();

    public void SetResources(IEnumerable<CurriculumResource> resources)
    {
        _resources.Clear();
        var index = 0;
        foreach (var resource in resources)
        {
            resource.CurriculumItemId = Id;
            resource.UpdateOrder(index++);
            _resources.Add(resource);
        }
    }

    public void ReorderResources(List<Guid> orderedAssetOrResourceIds)
    {
        var temp = _resources.ToList();
        _resources.Clear();

        for (int i = 0; i < orderedAssetOrResourceIds.Count; i++)
        {
            var targetId = orderedAssetOrResourceIds[i];
            var res = temp.FirstOrDefault(r => r.Id == targetId || r.CourseAssetId == targetId);
            if (res != null)
            {
                res.UpdateOrder(i);
                _resources.Add(res);
            }
        }

        foreach (var res in temp.Where(r => !_resources.Contains(r)))
        {
            res.UpdateOrder(_resources.Count);
            _resources.Add(res);
        }
    }

    public void UpdateOrder(int newOrder) => Order = newOrder;

    internal ErrorOr<Success> Delete()
    {
        if (IsDeleted)
            return Error.Failure("CurriculumItem.Failure", "Item is already deleted.");
        
        IsDeleted = true;
        OnDeleted = DateTime.UtcNow;
        return Result.Success;
    }

    internal ErrorOr<Success> Restore()
    {
        if (!IsDeleted)
            return Error.Failure("CurriculumItem.Failure", "Item is not deleted.");

        IsDeleted = false;
        OnDeleted = null;
        return Result.Success;
    }
}
