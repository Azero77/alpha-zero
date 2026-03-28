namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public record VideoMetadata(
    string OriginalFileName,
    string ContentType,
    long FileSize
);

public record VideoSpecifications
{
    public TimeSpan Duration { get; init; }
    public Resolution Resolution { get; init; } = null!;

    private VideoSpecifications() { } // EF Core

    public VideoSpecifications(TimeSpan duration, Resolution resolution)
    {
        Duration = duration;
        Resolution = resolution;
    }
    public static VideoSpecifications Empty => new VideoSpecifications();
}

public record Resolution
{
    public int width { get; init; }
    public int height { get; init; }

    private Resolution() { } // EF Core

    public Resolution(int width, int height)
    {
        this.width = width;
        this.height = height;
    }
}