using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using ErrorOr;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public interface ICourseAssetReadinessStrategy
{
    CourseAssetType AssetType { get; }
    ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload);
}

public class VideoAssetReadinessStrategy : ICourseAssetReadinessStrategy
{
    public CourseAssetType AssetType => CourseAssetType.Video;

    public ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload)
    {
        if (asset is not VideoCourseAsset video)
            return Error.Validation("Strategy.TypeMismatch", "Asset is not a VideoCourseAsset.");

        if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
        {
            var el = payload.Value;
            var relativeUrl = el.TryGetProperty("relativeUrl", out var u) ? u.GetString() : null;
            var thumbnailUrl = el.TryGetProperty("thumbnailUrl", out var t) ? t.GetString() : null;
            TimeSpan duration = TimeSpan.Zero;
            if (el.TryGetProperty("duration", out var d) && d.GetString() is string durStr && TimeSpan.TryParse(durStr, out var parsedDur))
            {
                duration = parsedDur;
            }

            video.UpdateStreamingInfo(duration, relativeUrl, thumbnailUrl);
        }

        return Result.Success;
    }
}

public class DocumentAssetReadinessStrategy : ICourseAssetReadinessStrategy
{
    public CourseAssetType AssetType => CourseAssetType.Document;

    public ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload)
    {
        if (asset is not DocumentCourseAsset doc)
            return Error.Validation("Strategy.TypeMismatch", "Asset is not a DocumentCourseAsset.");

        if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
        {
            var el = payload.Value;
            var fileName = el.TryGetProperty("fileName", out var f) ? f.GetString() ?? doc.FileName : doc.FileName;
            var size = el.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : doc.Size;
            var contentType = el.TryGetProperty("contentType", out var c) ? c.GetString() ?? doc.ContentType : doc.ContentType;

            doc.UpdateFileInfo(fileName, size, contentType);
        }

        return Result.Success;
    }
}

public class AssessmentAssetReadinessStrategy : ICourseAssetReadinessStrategy
{
    public CourseAssetType AssetType => CourseAssetType.Assessment;

    public ErrorOr<Success> ApplyReadiness(CourseAsset asset, JsonElement? payload)
    {
        if (asset is not AssessmentCourseAsset assessment)
            return Error.Validation("Strategy.TypeMismatch", "Asset is not an AssessmentCourseAsset.");

        if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
        {
            var el = payload.Value;
            var questionsNumber = el.TryGetProperty("questionsNumber", out var q) && q.TryGetInt32(out var qn) ? qn : assessment.QuestionsNumber;
            var type = assessment.Type;
            if (el.TryGetProperty("type", out var t) && Enum.TryParse<AssessmentType>(t.GetString(), out var parsedType))
            {
                type = parsedType;
            }

            assessment.UpdateAssessmentInfo(questionsNumber, type);
        }

        return Result.Success;
    }
}
