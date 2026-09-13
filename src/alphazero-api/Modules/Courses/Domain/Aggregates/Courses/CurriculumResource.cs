using AlphaZero.Shared.Domain;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CurriculumResource
{
    public CourseAsset Asset {get;private set;}
    public int Order { get; internal set; }
    public JsonElement Metadata { get; private set; }

    // Private constructor for EF Core
    private CurriculumResource() { }

    public CurriculumResource(CourseAsset asset, int order, JsonElement metadata)
    {
        Asset = asset;
        Order = order;
        Metadata = metadata;
    }

    internal ErrorOr<Success> UpdateOrder(int order)
    {
        if(order <= 0)
            return Error.Validation("Courses.Resources.Order.Validation","Order Can't be zero or negative");
        Order = order;
        return Result.Success;
    }

    internal void UpdateMetadata(JsonElement metadata)
    {
        Metadata = metadata;
    }
}
