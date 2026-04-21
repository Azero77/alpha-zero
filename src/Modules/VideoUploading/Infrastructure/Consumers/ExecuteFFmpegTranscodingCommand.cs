namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public record ExecuteFFmpegTranscodingCommand(
    Guid VideoId, 
    string SourceKey, 
    string DestinationPrefix, 
    int SourceWidth, 
    int SourceHeight,
    string? EncryptionMethod = "None");
