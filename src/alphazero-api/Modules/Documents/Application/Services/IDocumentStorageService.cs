namespace AlphaZero.Modules.Documents.Application.Services;

public interface IDocumentStorageService
{
    Task<string> GenerateUploadPresignedUrlAsync(string key, string contentType, TimeSpan expiresIn, Dictionary<string, string>? metadata = null);
    Task<string> GenerateDownloadPresignedUrlAsync(string key, string fileName, TimeSpan expiresIn);
    Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default);
}
