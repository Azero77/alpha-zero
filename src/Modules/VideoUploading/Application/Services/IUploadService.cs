using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Services;

public interface IUploadService
{
    Task<ErrorOr<GetPresignedUrlResponse>> GetFile(string key);
    Task<List<GetPresignedUrlResponse?>> GetFiles(List<string> keysAsList);
    Task<ErrorOr<Success>> DeleteFile(string key);
    Task<ErrorOr<GetPresignedUrlResponse>> UploadFile(string fileName, string contentType, Dictionary<string, string>? metadata = null);
    Task<ErrorOr<Dictionary<string, object>>> GetMetadata(string key);
}


public record GetPresignedUrlResponse(string key, string presignedUrl);
