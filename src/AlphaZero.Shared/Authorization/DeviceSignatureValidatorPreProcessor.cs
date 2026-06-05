using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Shared.Authorization;

public class DeviceSignatureValidatorPreProcessor(IDeviceSignatureVerifier verifier, IDeviceProvider deviceProvider) : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var globalRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
        var tenantScopedRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();
        
        // Only enforce for endpoints requiring IAM authorization
        if (globalRequirement is null && tenantScopedRequirement is null) return;

        var deviceId = context.HttpContext.Request.Headers["X-Device-Id"].ToString();
        var timestamp = context.HttpContext.Request.Headers["X-Timestamp"].ToString();
        var signature = context.HttpContext.Request.Headers["X-Signature"].ToString();

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            // If it's a protected endpoint, we require these headers
            await context.HttpContext.Response.SendForbiddenAsync(ct);
            return;
        }

        var result = await verifier.VerifySignatureAsync(deviceId, timestamp, signature, context.HttpContext.Request.Path);

        if (result.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
            return;
        }

        // Signature verified, set the device ID for downstream context
        deviceProvider.SetDeviceId(deviceId);
    }
}
