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
    public string MainType { get; private set; } // "Video", "Document", "Article", "Quiz"
    public JsonElement Content { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; } = null;

    public static readonly string[] ValidMainTypes = ["Video", "Document", "Article", "Quiz"];
    private static readonly JsonElement EmptyArrayElement = JsonDocument.Parse("[]").RootElement.Clone();

    private readonly List<CurriculumResource> _resources = new();
    public IReadOnlyCollection<CurriculumResource> Resources => _resources.AsReadOnly();

    private CurriculumItem() : base(default, default)
    {
        Title = string.Empty;
        MainType = "Video";
        Content = EmptyArrayElement;
    } // EF Core

    public CurriculumItem(Guid id, Guid tenantId, Guid sectionId, string title, int order, int bitIndex, string mainType, JsonElement? content = null)
        : base(id, tenantId)
    {
        SectionId = sectionId;
        Title = title;
        Order = order;
        BitIndex = bitIndex;
        MainType = NormalizeMainType(mainType);
        Content = content.HasValue && content.Value.ValueKind != JsonValueKind.Undefined ? content.Value.Clone() : EmptyArrayElement;
    }

    public static ErrorOr<CurriculumItem> Create(
        Guid id, Guid tenantId, Guid sectionId, string title, int order, int bitIndex, string mainType, JsonElement? content = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("CurriculumItem.Title", "Title is required.");

        if (!IsValidMainType(mainType))
            return Error.Validation("CurriculumItem.MainType",
                $"MainType must be one of: {string.Join(", ", ValidMainTypes)}.");

        return new CurriculumItem(id, tenantId, sectionId, title, order, bitIndex, NormalizeMainType(mainType), content);
    }

    public static bool IsValidMainType(string mainType)
    {
        if (string.IsNullOrWhiteSpace(mainType)) return false;
        return ValidMainTypes.Any(t => string.Equals(t, mainType, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMainType(string mainType)
    {
        var matched = ValidMainTypes.FirstOrDefault(t => string.Equals(t, mainType, StringComparison.OrdinalIgnoreCase));
        return matched ?? mainType;
    }

    public ErrorOr<Success> UpdateContent(string title, string mainType, JsonElement content)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("CurriculumItem.Title", "Title is required.");

        if (!IsValidMainType(mainType))
            return Error.Validation("CurriculumItem.MainType",
                $"MainType must be one of: {string.Join(", ", ValidMainTypes)}.");

        Title = title;
        MainType = NormalizeMainType(mainType);
        Content = content.ValueKind != JsonValueKind.Undefined ? content.Clone() : EmptyArrayElement;
        return Result.Success;
    }

    public static List<Guid> ExtractReferencedAssetIds(JsonElement content)
    {
        var assetIds = new List<Guid>();
        if (content.ValueKind != JsonValueKind.Array)
            return assetIds;

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;

            if (block.TryGetProperty("courseAssetId", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                if (Guid.TryParse(prop.GetString(), out var assetId) && assetId != Guid.Empty && !assetIds.Contains(assetId))
                {
                    assetIds.Add(assetId);
                }
            }
        }

        return assetIds;
    }

    internal void SyncResources(IEnumerable<CurriculumResource> resources)
    {
        _resources.Clear();
        _resources.AddRange(resources);
    }

    internal void ClearResources() => _resources.Clear();

    public ErrorOr<Success> AddResource(CourseAsset asset, JsonElement metadata)
    {
        // BOLA check: validate resource tenant matches item tenant
        if (asset.TenantId != TenantId)
            return Error.Validation("CurriculumItem.TenantMismatch", "Asset tenant must match item tenant.");

        // First resource validates type compatibility with MainType
        if (_resources.Count == 0)
        {
            if (!IsCompatibleType(MainType, asset.CourseAssetType))
                return Error.Validation("CurriculumItem.TypeMismatch", 
                    $"Asset type '{asset.CourseAssetType}' does not match item MainType '{MainType}'.");
        }

        var order = _resources.Count;
        var resource = new CurriculumResource(asset, order, metadata);
        _resources.Add(resource);
        return Result.Success;
    }

    public void ReorderResources(List<Guid> orderedAssetIds)
    {
        var temp = _resources.ToList();
        _resources.Clear();

        for (int i = 0; i < orderedAssetIds.Count; i++)
        {
            var res = temp.FirstOrDefault(r => r.CourseAssetId == orderedAssetIds[i]);
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

    private static bool IsCompatibleType(string mainType, CourseAssetType assetType)
    {
        return mainType.ToLowerInvariant() switch
        {
            "video" => assetType == CourseAssetType.Video,
            "quiz" or "assessment" => assetType == CourseAssetType.Assessment,
            "document" => assetType == CourseAssetType.Document,
            _ => false
        };
    }
}
