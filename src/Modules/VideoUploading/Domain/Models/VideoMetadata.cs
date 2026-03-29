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
    public static Resolution Empty => new Resolution(0,0);
}