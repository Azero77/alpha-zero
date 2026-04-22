using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Domain.Models;

public class VideoSecret : AggregateRoot
{
    public Guid VideoId { get; private set; }
    public string KeyId { get; private set; } = null!;
    public string KeyValue { get; private set; } = null!;
    public string? IV { get; private set; }

    private VideoSecret()
    {
        // EF Core
    }

    private VideoSecret(Guid videoId, string keyId, string keyValue, string? iv) : base(Guid.NewGuid())
    {
        VideoId = videoId;
        KeyId = keyId;
        KeyValue = keyValue;
        IV = iv;
    }

    public static VideoSecret Create(Guid videoId, string keyId, string keyValue, string? iv = null)
    {
        return new VideoSecret(videoId, keyId, keyValue, iv);
    }
}
