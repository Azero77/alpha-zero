namespace AlphaZero.Modules.VideoUploading.IntegrationEvents;

public record StartVideoProcessingCommand(string Key, string BucketName, Guid VideoId);
