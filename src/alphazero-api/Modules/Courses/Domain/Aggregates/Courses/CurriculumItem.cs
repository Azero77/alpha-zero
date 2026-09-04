using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CurriculumItem : TenantOwnedEntity, ISoftDeletable
{
    public string Title { get; private set; }
    public Guid SectionId { get; private set; }
    public int Order { get; internal set; } // UI Display Order
    public int BitIndex { get; private set; } // Immutable Bitmask Pointer
    public string MainType { get; private set; } // "Video", "Quiz", "Document", "Inline"
    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; } = null;

    private readonly List<CurriculumResource> _resources = new();
    public IReadOnlyCollection<CurriculumResource> Resources => _resources.AsReadOnly();
    public CurriculumItem(Guid id, Guid tenantId, Guid sectionId, string title, int order, int bitIndex, string mainType)
        : base(id, tenantId)
    {
        SectionId = sectionId;
        Title = title;
        Order = order;
        BitIndex = bitIndex;
        MainType = mainType;
    }

    public ErrorOr<Success> AddResource(ResourceArn arn, string type, JsonElement metadata)
    {
        // BOLA check: validate resource tenant matches active TenantId
        if (arn.TenantIdString != TenantId.ToString().ToLowerInvariant() && arn.TenantIdString != ResourceArn.GlobalTenant)
        {
            return Error.Validation("CurriculumItem.TenantMismatch", "Resource tenant must match active tenant.");
        }

        // Validate MainType invariant for primary resources
        if (type == "Primary")
        {
            var expectedService = GetExpectedServiceForMainType(MainType);
            if (!arn.Service.Equals(expectedService, StringComparison.OrdinalIgnoreCase))
            {
                return Error.Validation("CurriculumItem.ResourceTypeMismatch", $"Primary resource service '{arn.Service}' does not match item MainType '{MainType}'. Expected '{expectedService}'.");
            }
        }

        var order = _resources.Count;
        var resource = new CurriculumResource(arn, type, order, metadata);
        _resources.Add(resource);

        return Result.Success;
    }

    public void ReorderResources(List<ResourceArn> orderedArns)
    {
        var temp = _resources.ToList();
        _resources.Clear();

        for (int i = 0; i < orderedArns.Count; i++)
        {
            var res = temp.FirstOrDefault(r => r.Arn.Equals(orderedArns[i]));
            if (res != null)
            {
                res.UpdateOrder(i);
                _resources.Add(res);
            }
        }
        
        // Add any remaining resources that were not in the reordered list at the end
        foreach (var res in temp)
        {
            if (!_resources.Contains(res))
            {
                res.UpdateOrder(_resources.Count);
                _resources.Add(res);
            }
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

    private static string GetExpectedServiceForMainType(string mainType)
    {
        return mainType.ToLowerInvariant() switch
        {
            "video" => "video",
            "quiz" => "assessment",
            "assessment" => "assessment",
            "document" => "document",
            "inline" => "inline",
            _ => mainType.ToLowerInvariant()
        };
    }
}
