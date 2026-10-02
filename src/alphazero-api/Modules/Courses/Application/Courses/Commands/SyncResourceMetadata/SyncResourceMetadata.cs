using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;

public record SyncResourceMetadataCommand(Guid ResourceId, JsonElement Metadata) : ICommand<Success>;

public sealed class SyncResourceMetadataCommandHandler : IRequestHandler<SyncResourceMetadataCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly IEnumerable<ICourseMetadataSyncCommandHandler> _syncHandlers;
    private readonly ILogger<SyncResourceMetadataCommandHandler> _logger;

    public SyncResourceMetadataCommandHandler(
        IRepository<CourseAsset> assetRepository,
        IEnumerable<ICourseMetadataSyncCommandHandler> syncHandlers,
        ILogger<SyncResourceMetadataCommandHandler> logger)
    {
        _assetRepository = assetRepository;
        _syncHandlers = syncHandlers;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SyncResourceMetadataCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.ResourceId, ct);
        if (asset is null)
        {
            _logger.LogDebug("No course asset found for resource {ResourceId}", request.ResourceId);
            return Result.Success;
        }

        if ((request.Metadata.TryGetProperty("Title", out var titleProp) || request.Metadata.TryGetProperty("title", out titleProp))
            && titleProp.GetString() is string title && !string.IsNullOrWhiteSpace(title))
        {
            asset.UpdateTitle(title);
        }

        var handler = _syncHandlers.FirstOrDefault(h => h.SupportedType == asset.CourseAssetType);
        if (handler is not null)
        {
            await handler.Sync(asset, asset.ResourceArn, request.Metadata);
        }
        else
        {
            _logger.LogWarning("No metadata sync handler found for asset type {AssetType}", asset.CourseAssetType);
        }

        _assetRepository.Update(asset);
        _logger.LogInformation("Synchronized metadata for Resource {ResourceId}.", request.ResourceId);

        return Result.Success;
    }
}

public record VideoMetaData(string? ThumbnailUrl, string? RelativeStreamingUrl, TimeSpan Duration);
public record DocumentMetaData(string FileName, long FileSize, string ContentType);
public record AssessmentMetaData(int QuestionsNumber, AssessmentType Type);

public interface ICourseMetadataSyncCommandHandler
{
    CourseAssetType SupportedType { get; }
    Task Sync(CourseAsset oldAsset, ResourceArn arn, JsonElement payload);
}

public class VideoCourseMetadataSyncCommandHandler(ILogger<VideoCourseMetadataSyncCommandHandler> logger) : ICourseMetadataSyncCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CourseAssetType SupportedType => CourseAssetType.Video;

    public Task Sync(CourseAsset oldAsset, ResourceArn arn, JsonElement payload)
    {
        if (oldAsset is not VideoCourseAsset videoCourseAsset)
        {
            logger.LogError("Asset {Arn} is not of type video", arn);
            return Task.CompletedTask;
        }

        VideoMetaData? metadata = null;
        try
        {
            metadata = payload.Deserialize<VideoMetaData>(JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize VideoMetaData for arn {Arn}", arn);
        }

        var duration = metadata?.Duration ?? videoCourseAsset.Duration;
        if (duration == TimeSpan.Zero && (payload.TryGetProperty("duration", out var d) || payload.TryGetProperty("Duration", out d)))
        {
            if (d.GetString() is string dStr && TimeSpan.TryParse(dStr, out var parsedDur))
            {
                duration = parsedDur;
            }
        }

        var relativeUrl = metadata?.RelativeStreamingUrl;
        if (string.IsNullOrEmpty(relativeUrl) && (payload.TryGetProperty("relativeUrl", out var relProp) || payload.TryGetProperty("RelativeUrl", out relProp)))
        {
            relativeUrl = relProp.GetString();
        }
        relativeUrl ??= videoCourseAsset.RelativeStreamingUrl;

        var thumbnailUrl = metadata?.ThumbnailUrl;
        if (string.IsNullOrEmpty(thumbnailUrl) && (payload.TryGetProperty("thumbnailUrl", out var thumbProp) || payload.TryGetProperty("ThumbnailUrl", out thumbProp)))
        {
            thumbnailUrl = thumbProp.GetString();
        }
        thumbnailUrl ??= videoCourseAsset.ThumbnailUrl;

        videoCourseAsset.UpdateStreamingInfo(duration, relativeUrl, thumbnailUrl);
        return Task.CompletedTask;
    }
}

public class DocumentCourseMetadataSyncCommandHandler(ILogger<DocumentCourseMetadataSyncCommandHandler> logger) : ICourseMetadataSyncCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CourseAssetType SupportedType => CourseAssetType.Document;

    public Task Sync(CourseAsset oldAsset, ResourceArn arn, JsonElement payload)
    {
        if (oldAsset is not DocumentCourseAsset documentCourseAsset)
        {
            logger.LogError("Asset {Arn} is not of type document", arn);
            return Task.CompletedTask;
        }

        DocumentMetaData? metadata = null;
        try
        {
            metadata = payload.Deserialize<DocumentMetaData>(JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize DocumentMetaData for arn {Arn}", arn);
        }

        var fileName = metadata?.FileName 
            ?? (payload.TryGetProperty("fileName", out var f) || payload.TryGetProperty("FileName", out f) ? f.GetString() : null) 
            ?? documentCourseAsset.FileName;

        var fileSize = (metadata?.FileSize > 0 ? metadata.FileSize : (long?)null)
            ?? (payload.TryGetProperty("fileSize", out var s) || payload.TryGetProperty("FileSize", out s) || payload.TryGetProperty("size", out s) || payload.TryGetProperty("Size", out s) ? s.GetInt64() : documentCourseAsset.Size);

        var contentType = metadata?.ContentType 
            ?? (payload.TryGetProperty("contentType", out var c) || payload.TryGetProperty("ContentType", out c) ? c.GetString() : null) 
            ?? documentCourseAsset.ContentType;

        documentCourseAsset.UpdateFileInfo(fileName, fileSize, contentType);
        return Task.CompletedTask;
    }
}

public class AssessmentCourseMetadataSyncCommandHandler(ILogger<AssessmentCourseMetadataSyncCommandHandler> logger) : ICourseMetadataSyncCommandHandler
{
    public CourseAssetType SupportedType => CourseAssetType.Assessment;

    public Task Sync(CourseAsset oldAsset, ResourceArn arn, JsonElement payload)
    {
        if (oldAsset is not AssessmentCourseAsset assessmentCourseAsset)
        {
            logger.LogError("Asset {Arn} is not of type assessment", arn);
            return Task.CompletedTask;
        }

        var questionsNumber = assessmentCourseAsset.QuestionsNumber;
        if (payload.TryGetProperty("questionsNumber", out var q) || payload.TryGetProperty("QuestionsNumber", out q) ||
            payload.TryGetProperty("questionNumber", out q) || payload.TryGetProperty("QuestionNumber", out q))
        {
            if (q.TryGetInt32(out var qVal))
                questionsNumber = qVal;
        }

        var type = assessmentCourseAsset.Type;
        if (payload.TryGetProperty("type", out var t) || payload.TryGetProperty("Type", out t))
        {
            var typeStr = t.GetString();
            if (!string.IsNullOrWhiteSpace(typeStr) && Enum.TryParse<AssessmentType>(typeStr, true, out var parsedType))
            {
                type = parsedType;
            }
        }

        assessmentCourseAsset.UpdateAssessmentInfo(questionsNumber, type);
        return Task.CompletedTask;
    }
}