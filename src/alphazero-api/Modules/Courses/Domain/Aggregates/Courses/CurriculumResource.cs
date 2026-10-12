using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public abstract class CurriculumResource : Entity
{
    public Guid CurriculumItemId { get; internal set; }
    public int Order { get; internal set; }
    public Guid? CourseAssetId { get; protected set; }
    public CourseAsset? Asset { get; protected set; }
    public string ResourceType { get; protected set; } = null!;

    protected CurriculumResource() { } // EF Core

    protected CurriculumResource(Guid id, int order, string resourceType, Guid? courseAssetId = null, CourseAsset? asset = null)
        : base(id)
    {
        Order = order;
        ResourceType = resourceType;
        CourseAssetId = courseAssetId ?? asset?.Id;
        Asset = asset;
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
    public Guid VideoId { get; private set; }
    public double DurationSeconds { get; private set; }

    private VideoCurriculumResource() { ResourceType = "Video"; } // EF Core

    public VideoCurriculumResource(Guid id, int order, Guid courseAssetId, Guid videoId, double durationSeconds = 0, CourseAsset? asset = null)
        : base(id, order, "Video", courseAssetId, asset)
    {
        VideoId = videoId;
        DurationSeconds = durationSeconds;
    }

    public bool CanComplete(double watchedSeconds) =>
        DurationSeconds <= 0 || (watchedSeconds / DurationSeconds) >= 0.8;
}

public class DocumentCurriculumResource : CurriculumResource
{
    public Guid DocumentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string DocumentType { get; private set; } = "pdf";
    public bool IsPublic { get; private set; }
    public string? VariantName { get; private set; }

    private DocumentCurriculumResource() { ResourceType = "Document"; } // EF Core

    public DocumentCurriculumResource(
        Guid id,
        int order,
        Guid documentId,
        string title,
        string documentType = "pdf",
        bool isPublic = true,
        string? variantName = null,
        Guid? courseAssetId = null,
        CourseAsset? asset = null)
        : base(id, order, "Document", courseAssetId, asset)
    {
        DocumentId = documentId;
        Title = title;
        DocumentType = documentType;
        IsPublic = isPublic;
        VariantName = variantName;
    }

    public string DocUri => $"doc://{(IsPublic ? "public" : "private")}/{DocumentId}{(string.IsNullOrEmpty(VariantName) ? "" : $"?variantName={VariantName}")}";

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
}

public class ArticleCurriculumResource : CurriculumResource
{
    public string TextContent { get; private set; } = string.Empty;

    private ArticleCurriculumResource() { ResourceType = "Article"; } // EF Core

    public ArticleCurriculumResource(Guid id, int order, string textContent, Guid? courseAssetId = null, CourseAsset? asset = null)
        : base(id, order, "Article", courseAssetId, asset)
    {
        TextContent = textContent;
    }

    public void UpdateContent(string textContent)
    {
        TextContent = textContent;
    }
}

public class QuizCurriculumResource : CurriculumResource
{
    public Guid AssessmentId { get; private set; }
    public int PassingScore { get; private set; }

    private QuizCurriculumResource() { ResourceType = "Quiz"; } // EF Core

    public QuizCurriculumResource(Guid id, int order, Guid assessmentId, int passingScore = 80, Guid? courseAssetId = null, CourseAsset? asset = null)
        : base(id, order, "Quiz", courseAssetId, asset)
    {
        AssessmentId = assessmentId;
        PassingScore = passingScore;
    }

    public bool CanComplete(int score) => score >= PassingScore;
}
