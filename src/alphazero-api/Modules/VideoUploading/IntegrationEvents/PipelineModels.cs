using System;

namespace AlphaZero.Modules.VideoUploading.IntegrationEvents;

// ==========================================
// Step Functions Payload DTOs
// ==========================================

public record S3VideoCreatedEventParserInput(
    string BucketName,
    string SourceKey);

public record S3VideoCreatedEventParserOutput(
    string VideoId,
    string TenantId,
    string SourceBucket,
    string? Description,
    string FileName,
    string Title,
    string SourceKey,
    string? TargetResourceArn = null,
    string? TranscodingEngine = "FFMPEG",
    string? EncryptionMethod = "ClearKey",
    bool IsDefaultThumbnail = true);

public record VideoAnalyzerInput(
    string VideoId,
    string TenantId,
    string SourceBucket,
    string SourceKey,
    string? TargetResourceArn = null,
    string? TranscodingEngine = "FFMPEG",
    string? EncryptionMethod = "ClearKey",
    bool IsDefaultThumbnail = true);

public record SourceMetadata(
    int SourceWidth,
    int SourceHeight,
    double DurationSeconds,
    string DurationFormatted,
    double FrameRate,
    string AspectRatio,
    string VideoCodec,
    string AudioCodec,
    int AudioBitrateKbps,
    int AudioSampleRate);

public record VideoAnalyzerOutput(
    int SourceWidth,
    int SourceHeight,
    double DurationSeconds,
    string DurationFormatted,
    double FrameRate,
    string AspectRatio,
    string VideoCodec,
    string AudioCodec,
    int AudioBitrateKbps,
    int AudioSampleRate);

public record JobPreparerInput(
    string VideoId,
    string TenantId,
    string SourceBucket,
    string SourceKey,
    string TransientOutputBucket,
    SourceMetadata SourceMetadata,
    string? TargetResourceArn = null,
    string? TranscodingEngine = "FFMPEG",
    string? EncryptionMethod = "ClearKey",
    bool IsDefaultThumbnail = true,
    string? CustomThumbnailKey = null);

public record JobPreparerOutput(
    string JobConfigS3Uri,
    string JobConfigKey,
    string SourceBucket,
    string SourceKey,
    string TransientOutputBucket,
    string OutputPrefix,
    string TranscodingEngine,
    int SourceWidth,
    int SourceHeight,
    string DurationFormatted,
    string? TargetResourceArn = null);

// ==========================================
// Transcoder Job Config DTOs (JobPreparer output JSON)
// ==========================================

public sealed record TranscodingJobInput
{
    public Guid VideoId { get; init; }
    public Guid TenantId { get; init; }
    public string SourcePath { get; init; } = "";
    public string OutputPrefix { get; init; } = "";
    public VideoMetadata SourceMetadata { get; init; } = default!;
    public TranscodeSettings Settings { get; init; } = default!;
    public EncryptionMethod EncryptionMethod { get; init; } = EncryptionMethod.None;
    public EncryptionSettings? Encryption { get; init; }
    public string? ThumbnailRelativeUrl { get; init; }
}

public sealed record VideoMetadata(
    int SourceWidth,
    int SourceHeight,
    TimeSpan Duration);

public sealed record TranscodeSettings
{
    public OutputPreset[] Outputs { get; init; } = Array.Empty<OutputPreset>();
    public int SegmentLengthSeconds { get; init; } = 6;
    public int FragmentLengthSeconds { get; init; } = 2;
    public AudioSettings Audio { get; init; } = AudioSettings.Default;
}

public sealed record OutputPreset(
    int Width,
    int Height,
    int MaxBitrateKbps,
    int QvbrQualityLevel,
    string NameModifier);

public sealed record AudioSettings(
    string Codec,
    int BitrateKbps,
    int SampleRate)
{
    public static readonly AudioSettings Default = new("aac", 128, 44100);
}

public sealed record EncryptionSettings(
    string KeyId,
    string Key,
    string? KeyUrl);

public enum EncryptionMethod
{
    None = 0,
    ClearKey = 1
}

// ==========================================
// SQS Message DTOs
// ==========================================

public record VideoProgressQueueMessage(
    string VideoId,
    string TenantId,
    string Stage,
    string Status,
    string? Metadata = null);

public record VideoPublishedQueueMessage(
    Guid VideoId,
    Guid TenantId,
    string Status,
    string PlaybackUrl,
    string? ThumbnailUrl,
    string? Duration,
    int? Width,
    int? Height,
    string? EngineUsed,
    string? TargetResourceArn);

public record VideoProcessingErrorDetail(
    string? ErrorType, 
    string? Cause);

public record VideoProcessingFailedQueueMessage(
    Guid VideoId,
    Guid TenantId,
    string Status,
    VideoProcessingErrorDetail? Error,
    string? TargetResourceArn);
