using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public static class VideoErrors
{
    public static Error InvalidStatus => Error.Conflict(
        code: "Video.InvalidStatus",
        description: "The video is not in a valid state for this operation.");

    public static Error EmptyTitle => Error.Validation(
        code: "Video.EmptyTitle",
        description: "Video title cannot be empty.");
}
