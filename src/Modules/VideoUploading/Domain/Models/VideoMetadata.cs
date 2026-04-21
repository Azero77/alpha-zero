namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public class VideoMetadata : IEquatable<VideoMetadata>
{
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSize { get; private set; }
    public string TranscodingMethod { get; private set; } = null!;
    public string? EncryptionMethod { get; private set; } = "None";

    private VideoMetadata()
    {
        // EF Core
    }

    public VideoMetadata(
        string originalFileName, 
        string contentType, 
        long fileSize, 
        string transcodingMethod, 
        string? encryptionMethod = "None")
    {
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSize = fileSize;
        TranscodingMethod = transcodingMethod;
        EncryptionMethod = encryptionMethod;
    }

    public override bool Equals(object? obj) => Equals(obj as VideoMetadata);

    public bool Equals(VideoMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return OriginalFileName == other.OriginalFileName &&
               ContentType == other.ContentType &&
               FileSize == other.FileSize &&
               TranscodingMethod == other.TranscodingMethod &&
               EncryptionMethod == other.EncryptionMethod;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(OriginalFileName, ContentType, FileSize, TranscodingMethod, EncryptionMethod);
    }

    public static bool operator ==(VideoMetadata? left, VideoMetadata? right) => Equals(left, right);
    public static bool operator !=(VideoMetadata? left, VideoMetadata? right) => !Equals(left, right);
}

public record VideoSpecifications
{
    public TimeSpan Duration { get; init; }
    public Resolution Resolution { get; init; } = null!;

    private VideoSpecifications() { }

    public VideoSpecifications(TimeSpan duration, Resolution resolution)
    {
        Duration = duration;
        Resolution = resolution;
    }
    public static VideoSpecifications Empty => new VideoSpecifications(TimeSpan.Zero,Resolution.Empty);
}

public record Resolution
{
    public int width { get; init; }
    public int height { get; init; }

    public Resolution(int width, int height)
    {
        this.width = width;
        this.height = height;
    }
    private Resolution() { }
    public static Resolution Empty => new Resolution(0,0);
}
