using AlphaZero.Shared.Domain;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public abstract class CurriculumResource : Entity
{
    public Guid CurriculumItemId { get; internal set; }
    public int Order { get; internal set; }
    public Guid? CourseAssetId { get; protected set; } 
    public abstract ResourceType ResourceType { get; }

    protected CurriculumResource() { } // EF Core

    protected CurriculumResource(Guid id, int order, Guid? courseAssetId = null)
        : base(id)
    {
        Order = order;
        CourseAssetId = courseAssetId;
    }

    public ErrorOr<Success> UpdateOrder(int order)
    {
        if (order < 0)
            return Error.Validation("CurriculumResource.Order", "Order cannot be negative.");
        Order = order;
        return Result.Success;
    }
}

public class VideoCurriculumResource : CurriculumResource
{
    public double DurationSeconds { get; private set; }

    private VideoCurriculumResource() { } // EF Core

    public VideoCurriculumResource(Guid id, int order, Guid courseAssetId, double durationSeconds = 0)
        : base(id, order, courseAssetId)
    {
        DurationSeconds = durationSeconds;
    }

    public bool CanComplete(double watchedSeconds) =>
        DurationSeconds <= 0 || (watchedSeconds / DurationSeconds) >= 0.8;

    public override ResourceType ResourceType => ResourceType.Video;
}

public class DocumentCurriculumResource : CurriculumResource
{
    public string Title { get; private set; } = string.Empty;
    public string DocumentType { get; private set; } = "pdf";
    public bool IsPublic { get; private set; }
    public string? VariantName { get; private set; }

    private DocumentCurriculumResource()
    {
        
    }
    public DocumentCurriculumResource(
        Guid id,
        int order,
        string title,
        Guid courseAssetId,
        string documentType = "pdf",
        bool isPublic = true,
        string? variantName = null
        )
        : base(id, order, courseAssetId)
    {
        Title = title;
        DocumentType = documentType;
        IsPublic = isPublic;
        VariantName = variantName;
    }

    public static ErrorOr<DocumentCurriculumResource> Create(Guid id,
        int order,
        string title,
        Guid courseAssetId,
        string documentType = "pdf",
        bool isPublic = true,
        string? variantName = null)
    {
        if (courseAssetId == Guid.Empty)
            return Error.Validation("Course.Items.Document.Validation", "Guid Item Can't Be Empty");

        return new  DocumentCurriculumResource(id,order,title,courseAssetId, documentType, isPublic, variantName);
    } 

    public string DocUri => $"doc://{(IsPublic ? "public" : "private")}/{CourseAssetId!}{(string.IsNullOrEmpty(VariantName) ? "" : $"?variantName={VariantName}")}";

    public static (bool IsPublic, Guid DocumentId, string? VariantName) ParseDocUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith("doc://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid doc URI format.", nameof(uri));

        var withoutScheme = uri.Substring(6);
        var queryIndex = withoutScheme.IndexOf('?');
        var path = queryIndex >= 0 ? withoutScheme.Substring(0, queryIndex) : withoutScheme;
        var query = queryIndex >= 0 ? withoutScheme.Substring(queryIndex + 1) : null;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var isPublic = parts.Length > 0 && parts[0].Equals("public", StringComparison.OrdinalIgnoreCase);
        var docIdStr = parts.Length > 1 ? parts[1] : (parts.Length > 0 ? parts[0] : "");
        _ = Guid.TryParse(docIdStr, out var docId);

        string? variant = null;
        if (!string.IsNullOrEmpty(query))
        {
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=');
                if (kv.Length == 2 && kv[0].Equals("variantName", StringComparison.OrdinalIgnoreCase))
                    variant = kv[1];
            }
        }

        return (isPublic, docId, variant);
    }

    public override ResourceType ResourceType => ResourceType.Document;
}

public class ArticleCurriculumResource : CurriculumResource
{
    public JsonElement Content { get; private set; }
    public string TextContent => Content.ValueKind == JsonValueKind.String ? Content.GetString() ?? string.Empty : Content.ToString();
    private ArticleCurriculumResource() { }

    public ArticleCurriculumResource(Guid id, int order, JsonElement content, Guid? courseAssetId = null )
        : base(id, order, courseAssetId)
    {
        Content = content;
    }
    public void UpdateContent(JsonElement content)
    {
        Content = content;
    }

    public override ResourceType ResourceType => ResourceType.Article;
}

public class AssessmentCurriculumResource : CurriculumResource
{
    public int PassingScore { get;  }

    private AssessmentCurriculumResource() { } 

    public AssessmentCurriculumResource(Guid id, int order, int passingScore, Guid courseAssetId)
        : base(id, order, courseAssetId)
    {
        PassingScore = passingScore;
    }

    public bool CanComplete(int score) => score >= PassingScore;
    public override ResourceType ResourceType => ResourceType.Assessment;
}

public enum ResourceType
{
    Assessment,
    Article,
    Document,
    Video
}
