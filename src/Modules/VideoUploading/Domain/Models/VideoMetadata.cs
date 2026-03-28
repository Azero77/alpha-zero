namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public record VideoMetadata(
    string OriginalFileName,
    string ContentType,
    long FileSize
);

public record VideoSpecifications(
    TimeSpan Duration ,
    Resolution Resolution);

public record Resolution(int width,int height);