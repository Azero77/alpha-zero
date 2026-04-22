using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class DefaultVideoEncryptionService(AppDbContext dbContext, Microsoft.Extensions.Configuration.IConfiguration configuration) : IVideoEncryptionService
{
    public async Task<ErrorOr<EncryptionParams>> GetEncryptionParamsAsync(
        Guid videoId, 
        VideoEncryptionMethod method, 
        CancellationToken ct = default)
    {
        if (method == VideoEncryptionMethod.None)
        {
            return Error.Validation("Encryption.NotRequired", "No encryption required for this method.");
        }

        string baseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7016";

        if (method == VideoEncryptionMethod.ClearKey)
        {
            string keyUrl = $"{baseUrl.TrimEnd('/')}/api/video/keys/{videoId}";

            // 1. Check if key already exists
            var existingSecret = await dbContext.VideoSecrets
                .FirstOrDefaultAsync(s => s.VideoId == videoId, ct);

            if (existingSecret != null)
            {
                return new EncryptionParams(
                    existingSecret.KeyId, 
                    existingSecret.KeyValue, 
                    keyUrl); 
            }

            // 2. Generate a 16-byte random key for AES-128
            var keyBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            string keyId = videoId.ToString("N"); // No dashes
            string keyValue = Convert.ToHexString(keyBytes).ToLower();
            
            // 3. Persist the secret
            var newSecret = VideoSecret.Create(videoId, keyId, keyValue);
            await dbContext.VideoSecrets.AddAsync(newSecret, ct);
            await dbContext.SaveChangesAsync(ct);

            // 4. Return params
            return new EncryptionParams(keyId, keyValue, keyUrl);
        }

        if (method == VideoEncryptionMethod.DRM)
        {
            // This is a placeholder for SPEKE/DRM integration
            return Error.Failure("Encryption.DRMNotImplemented", "DRM/SPEKE encryption is not yet implemented.");
        }

        return Error.Failure("Encryption.UnsupportedMethod", $"Unsupported encryption method: {method}");
    }
}
