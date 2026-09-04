using AlphaZero.Shared.Domain;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CurriculumResource
{
    public ResourceArn Arn { get; private set; }
    public string Type { get; private set; } // "Primary" or "Auxiliary"
    public int Order { get; internal set; }
    public JsonElement Metadata { get; private set; }

    // Private constructor for EF Core
    private CurriculumResource() { }

    public CurriculumResource(ResourceArn arn, string type, int order, JsonElement metadata)
    {
        Arn = arn ?? throw new ArgumentNullException(nameof(arn));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Order = order;
        Metadata = metadata;
    }

    internal void UpdateOrder(int order)
    {
        Order = order;
    }

    internal void UpdateMetadata(JsonElement metadata)
    {
        Metadata = metadata;
    }
}
