using AlphaZero.Shared.Authorization;
using ErrorOr;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace AlphaZero.Modules.Identity.Domain.Services;

public class DeviceSignatureVerifier(IPublicKeyProvider publicKeyProvider, ILogger<DeviceSignatureVerifier> logger) : IDeviceSignatureVerifier
{
    public async Task<ErrorOr<Success>> VerifySignatureAsync(string deviceId, string timestamp, string signature, string path)
    {
        // 1. Replay attack prevention
        if (!long.TryParse(timestamp, out var ts))
            return Error.Validation("Device.InvalidTimestamp", "Timestamp must be a number.");

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        if (DateTimeOffset.UtcNow - requestTime > TimeSpan.FromMinutes(5))
            return Error.Validation("Device.TimestampExpired", "Request timestamp is too old.");

        // 2. Get Public Key
        var publicKeyPem = await publicKeyProvider.GetPublicKeyAsync(deviceId);
        if (string.IsNullOrEmpty(publicKeyPem))
        {
            logger.LogWarning("Public key not found for device {DeviceId}", deviceId);
            return Error.NotFound("Device.KeyNotFound", "Public key not found for this device.");
        }

        // 3. Verify Signature
        try
        {
            var dataToVerify = $"{path}:{timestamp}";
            var dataBytes = Encoding.UTF8.GetBytes(dataToVerify);
            var signatureBytes = Convert.FromBase64String(signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            var isValid = rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (!isValid)
            {
                logger.LogWarning("Invalid signature for device {DeviceId}", deviceId);
                return Error.Forbidden("Device.InvalidSignature", "Signature verification failed.");
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying signature for device {DeviceId}", deviceId);
            return Error.Failure("Device.VerificationError", "An error occurred during signature verification.");
        }
    }
}
