using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

using System.Text;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Aspire.Shared;
using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class DefaultVideoEncryptionService(
    AppDbContext dbContext, 
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    IAmazonSimpleSystemsManagement ssmClient,
    AWSResources awsResources,
    HybridCache cache) : IVideoEncryptionService
{
    public async Task<ErrorOr<byte[]>> GetClearKeyAsync(Guid videoId, CancellationToken ct = default)
    {
        var parameterName = awsResources.MasterClearKeyParameter 
            ?? "/AlphaZero/VideoPipeline/MasterClearKey";

        var masterSecret = await cache.GetOrCreateAsync(
            "ssm:master-clear-key",
            async cancel =>
            {
                var response = await ssmClient.GetParameterAsync(new GetParameterRequest
                {
                    Name = parameterName,
                    WithDecryption = true
                }, cancel);
                return response.Parameter.Value;
            }, cancellationToken: ct);

        if (string.IsNullOrEmpty(masterSecret))
            return Error.Failure("Encryption.KeyMissing", "Master ClearKey not found.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(masterSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(videoId.ToString()));
        
        return hash[..16];
    }

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
