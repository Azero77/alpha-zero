namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public record VideoMetadata
{
    public string OriginalFileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long FileSize { get; init; }
    public string TranscodingMethod { get; init; } = null!;
    public string? EncryptionMethod { get; init; } = "None";

    private VideoMetadata()
    {
        // EF Core
    }

    public VideoMetadata(
        string originalFileName, 
        string contentType, 
        long fileSize, 
        string transcodingMethod, 
        string? encryptionMethod = "None",
        string? encryptionKey = null,
        string? encryptionKeyId = null)
    {
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSize = fileSize;
        TranscodingMethod = transcodingMethod;
        EncryptionMethod = encryptionMethod;
    }
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
