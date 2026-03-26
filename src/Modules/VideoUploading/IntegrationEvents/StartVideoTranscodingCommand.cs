namespace AlphaZero.Modules.VideoUploading.IntegrationEvents;

public record StartVideoTranscodingCommand(string Key, string BucketName, Guid VideoId);
