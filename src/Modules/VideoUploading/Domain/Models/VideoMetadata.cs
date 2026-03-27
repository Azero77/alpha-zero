namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public record VideoMetadata(
    string OriginalFileName,
    string ContentType,
    long FileSize,
    TimeSpan? Duration = null,
    string? Resolution = null
);
