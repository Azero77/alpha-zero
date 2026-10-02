using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CurriculumResource
{
    public Guid CourseAssetId { get; private set; }
    public CourseAsset Asset { get; private set; }
    public int Order { get; internal set; }
    public JsonElement Metadata { get; private set; }

    private CurriculumResource() { } // EF Core

    public CurriculumResource(CourseAsset asset, int order, JsonElement metadata)
    {
        CourseAssetId = asset.Id;
        Asset = asset;
        Order = order;
        Metadata = metadata;
    }

    internal ErrorOr<Success> UpdateOrder(int order)
    {
        if (order < 0)
            return Error.Validation("Courses.Resources.Order.Validation", "Order cannot be negative.");
        Order = order;
        return Result.Success;
    }

    internal void UpdateMetadata(JsonElement metadata)
    {
        Metadata = metadata;
    }
}
