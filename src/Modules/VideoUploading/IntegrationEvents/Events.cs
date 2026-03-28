namespace AlphaZero.Modules.VideoUploading.IntegrationEvents;

/* 
 * COMMANDS (Instructions to do something)
 * These are sent to specific consumers to perform a task.
 */

public record AnalyzeVideoCommand(Guid VideoId, string Key, string BucketName);

public record TranscodeVideoCommand(Guid VideoId, string Key, string BucketName, int Width, int Height);

public record SyncVideoToCdnCommand(Guid VideoId, string S3KeyPrefix, string S3Bucket);


/* 
 * EVENTS (Facts about what happened)
 * These are published to let the system know a stage is complete.
 */

// PHASE 1: INGESTION
public record UploadVideoRequestedEvent(Guid VideoId, Guid TenantId, DateTime OnTime);
public record VideoDeliveredToInputEvent(Guid VideoId, string Key, string BucketName, Guid TenantId);

// PHASE 2: ANALYSIS
public record VideoMetadataProcessedEvent(Guid VideoId, TimeSpan Duration, int Width, int Height);

// PHASE 3: TRANSCODING
public record VideoTranscodingStartedEvent(Guid VideoId, string JobId);
public record VideoTranscodingFinishedEvent(Guid VideoId, string OutputKeyPrefix, string BucketName);

// PHASE 4: DISTRIBUTION
public record VideoCdnSyncCompletedEvent(Guid VideoId, string R2PublicUrl);

// PHASE 5: FINALIZATION
public record VideoPublishedEvent(Guid VideoId, string FinalUrl);
public record VideoProcessingFailedEvent(Guid VideoId, string Reason, string? Key);

// LIFECYCLE
public record VideoDeletedFromS3Event(string Key);
public record VideoMetadataUpdatedEvent(string Key);
