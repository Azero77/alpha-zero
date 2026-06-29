using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VideoUploading.Tests.Integration.Abstractions;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class OfflineVideoKeyTests : BaseIntegrationTest
{
    private readonly RSA _rsa;
    private readonly string _publicKeyPem;

    public OfflineVideoKeyTests(ApiFactory factory) : base(factory)
    {
        _rsa = RSA.Create();
        _publicKeyPem = _rsa.ExportRSAPublicKeyPem();
    }

    private string GenerateSignature(string path, string timestamp)
    {
        var dataToVerify = $"{path}:{timestamp}";
        var dataBytes = Encoding.UTF8.GetBytes(dataToVerify);
        var signatureBytes = _rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signatureBytes);
    }

    [Fact]
    public async Task GetVideoKey_ShouldReturnForbidden_WhenRequestMissingSignature()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var videoId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "test-user", "Test User").Value;
        user.RegisterDevice("My Phone", DevicePlatform.Android, _publicKeyPem, DateTime.UtcNow);
        var deviceId = user.Devices.First().Id;
        user.SetMainDevice(deviceId, DateTime.UtcNow);
        DbContext.TenantUsers.Add(user);

        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "VideoPolicy", new() 
        { 
            ManagedPolicyStatement.Create("S1", new() { "video:Stream" }, true, condition) .Value
        });

        var principal = Principal.Create(Guid.NewGuid(), "video-role", "hash", "Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);
        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);

        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:video:{tenantId}:video/{videoId}", DateTime.UtcNow).Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        // Act - No signature headers
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/video/keys/{videoId}");
        request.Headers.Add("X-Test-User-Id", user.Id.ToString());
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await Client.SendAsync(request);

        // Assert
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "because unauthorized access should be blocked. Response: " + responseBody);
    }

    [Fact]
    public async Task GetVideoKey_ShouldPassAuthorization_WhenRequestHasValidSignatureFromMainDevice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var videoId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "test-user-2", "Test User").Value;
        user.RegisterDevice("My Phone", DevicePlatform.Android, _publicKeyPem, DateTime.UtcNow);
        var deviceId = user.Devices.First().Id;
        user.SetMainDevice(deviceId, DateTime.UtcNow);
        DbContext.TenantUsers.Add(user);

        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "VideoPolicy2", new() 
        { 
            ManagedPolicyStatement.Create("S1", new() { "video:Stream" }, true, condition) .Value
        });

        var principal = Principal.Create(Guid.NewGuid(), "video-role-2", "hash", "Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);
        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);

        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:video:{tenantId}:video/{videoId}", DateTime.UtcNow).Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        // Act - Valid headers
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = $"video/{videoId}";
        var signature = GenerateSignature(path, timestamp);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/video/keys/{videoId}");
        request.Headers.Add("X-Test-User-Id", user.Id.ToString());
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("X-Device-Id", deviceId.ToString());
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Signature", signature);

        var response = await Client.SendAsync(request);

        // Assert
        // We expect NotFound (404) because the video doesn't actually exist in the VideoUploading module's DB.
        // If authorization failed, it would have returned 403 Forbidden.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
