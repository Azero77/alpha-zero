using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public record ThumbnailInfo
{
    public string? CustomThumbnailKey { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool UseCustom { get; init; }
    public ThumbnailState State {get;init;}
    public ThumbnailInfo(string? customThumbnailKey, string? thumbnailUrl = null, bool useCustom = false, ThumbnailState state = ThumbnailState.Pending)
    {
        CustomThumbnailKey = customThumbnailKey;
        ThumbnailUrl = thumbnailUrl;
        UseCustom = useCustom;
        State =  state;
    }
    public static ThumbnailInfo Empty => new ThumbnailInfo(customThumbnailKey: null);
}

public enum ThumbnailState
{
    Pending,
    Delivered,
    Published
}
