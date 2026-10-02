using ErrorOr;

namespace AlphaZero.Shared.Authorization;

public interface IDeviceSignatureVerifier
{
    Task<ErrorOr<Success>> VerifySignatureAsync(string deviceId, string timestamp, string signature, string path);
}

public interface IPublicKeyProvider
{
    Task<string?> GetPublicKeyAsync(string deviceId, CancellationToken token = default);
    Task SetNewDevicePublicKey(string tenantUserId,string deviceId, string publicKey, CancellationToken token = default);
}
