namespace AlphaZero.Modules.Documents.Application.Services;

public interface IDocumentStorageService
{
    Task<string> GenerateUploadPresignedUrlAsync(string key, string contentType, TimeSpan expiresIn);
    Task<string> GenerateDownloadPresignedUrlAsync(string key, string fileName, TimeSpan expiresIn);
}
