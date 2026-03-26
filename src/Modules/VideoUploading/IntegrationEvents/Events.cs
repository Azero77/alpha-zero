namespace AlphaZero.Modules.VideoUploading.IntegrationEvents;


//Events
public record UploadVideoRequestedEvent(Guid VideoId, DateTime OnTime);


/// <summary>
/// Internal event representing a confirmed video upload
/// </summary>
public record VideoUploadedToInputEvent(string Key, string BucketName, Guid VideoId);
public record VideoProcessingStartedEvent(string Key,string BucketName,Guid VideoId,string JobId);
public record VideoProcessingCompletedEvent(Guid VideoId);
public record VideoPublishedEvent(Guid VideoId);
public record VideoUploadFailedEvent(Guid VideoId , string Key); //videoId maybe null to no upload // missing metadata problems
