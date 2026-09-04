using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Services;

public record EncryptionParams(
    string KeyId, 
    string KeyValue, 
    string? KeyUrl = null);

public interface IVideoEncryptionService
{
    Task<ErrorOr<EncryptionParams>> GetEncryptionParamsAsync(
        Guid videoId, 
        VideoEncryptionMethod method, 
        CancellationToken ct = default);
}
