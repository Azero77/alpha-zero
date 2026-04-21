using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using ErrorOr;
using System.Security.Cryptography;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class DefaultVideoEncryptionService : IVideoEncryptionService
{
    public Task<ErrorOr<EncryptionParams>> GetEncryptionParamsAsync(
        Guid videoId, 
        VideoEncryptionMethod method, 
        CancellationToken ct = default)
    {
        if (method == VideoEncryptionMethod.None)
        {
            return Task.FromResult<ErrorOr<EncryptionParams>>(Error.Validation("Encryption.NotRequired", "No encryption required for this method."));
        }

        if (method == VideoEncryptionMethod.ClearKey)
        {
            // Generate a 16-byte random key for AES-128
            var keyBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            string keyId = videoId.ToString("N"); // No dashes
            string keyValue = Convert.ToHexString(keyBytes).ToLower();
            
            // In a real scenario, this URL would point to a key-serving endpoint
            // For now, we use a placeholder that will be resolved at runtime or by the CDN.
            string keyUrl = $"https://api.alphazero.com/api/video/keys/{videoId}";

            return Task.FromResult<ErrorOr<EncryptionParams>>(new EncryptionParams(keyId, keyValue, keyUrl));
        }

        if (method == VideoEncryptionMethod.DRM)
        {
            // This is a placeholder for SPEKE/DRM integration
            return Task.FromResult<ErrorOr<EncryptionParams>>(Error.Failure("Encryption.DRMNotImplemented", "DRM/SPEKE encryption is not yet implemented."));
        }

        return Task.FromResult<ErrorOr<EncryptionParams>>(Error.Failure("Encryption.UnsupportedMethod", $"Unsupported encryption method: {method}"));
    }
}
