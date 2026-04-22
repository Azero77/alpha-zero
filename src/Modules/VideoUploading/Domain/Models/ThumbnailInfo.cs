namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public record ThumbnailInfo
{
    public string? CustomThumbnailKey { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool UseCustom { get; init; }

    private ThumbnailInfo() { }

    public ThumbnailInfo(string? customThumbnailKey, string? thumbnailUrl = null, bool useCustom = false)
    {
        CustomThumbnailKey = customThumbnailKey;
        ThumbnailUrl = thumbnailUrl;
        UseCustom = useCustom;
    }

    public static ThumbnailInfo Empty => new ThumbnailInfo(null);
}
