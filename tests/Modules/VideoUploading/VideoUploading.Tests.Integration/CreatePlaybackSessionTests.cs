using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Security;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoUploading.Tests.Integration.Abstractions;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class CreatePlaybackSessionIntegrationTests : BaseIntegrationTest
{
    private readonly RSA _rsa;
    private readonly string _publicKeyPem;

    public CreatePlaybackSessionIntegrationTests(ApiFactory factory) : base(factory)
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
    public async Task CreatePlaybackSession_ShouldReturnForbidden_WhenRequestMissingSignature()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var videoId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "playback-user-1", "Playback User 1").Value;
        user.RegisterDevice("My Phone", DevicePlatform.Android, _publicKeyPem, DateTime.UtcNow);
        var deviceId = user.Devices.First().Id;
        user.SetMainDevice(deviceId, DateTime.UtcNow);
        DbContext.TenantUsers.Add(user);

        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "VideoPlaybackPolicy", new()
        {
            ManagedPolicyStatement.Create("S1", new() { "video:Stream" }, true, condition).Value
        });

        var principal = Principal.Create(Guid.NewGuid(), "video-playback-role", "hash", "Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);
        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);

        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:video:{tenantId}:video/{videoId}", DateTime.UtcNow).Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        // Act: POST /api/videos/{videoId}/playback-session without signature headers
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/videos/{videoId}/playback-session")
        {
            Content = JsonContent.Create(new { VideoId = videoId })
        };
        request.Headers.Add("X-Test-User-Id", user.Id.ToString());
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePlaybackSession_ShouldPassAuthorization_AndReturnNotFound_WhenVideoDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var videoId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "playback-user-2", "Playback User 2").Value;
        user.RegisterDevice("My Tablet", DevicePlatform.Android, _publicKeyPem, DateTime.UtcNow);
        var deviceId = user.Devices.First().Id;
        user.SetMainDevice(deviceId, DateTime.UtcNow);
        DbContext.TenantUsers.Add(user);

        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "VideoPlaybackPolicy2", new()
        {
            ManagedPolicyStatement.Create("S1", new() { "video:Stream" }, true, condition).Value
        });

        var principal = Principal.Create(Guid.NewGuid(), "video-playback-role-2", "hash", "Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);
        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);

        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:video:{tenantId}:video/{videoId}", DateTime.UtcNow).Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        // Act: POST /api/videos/{videoId}/playback-session with valid signature headers
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = $"video/{videoId}";
        var signature = GenerateSignature(path, timestamp);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/videos/{videoId}/playback-session")
        {
            Content = JsonContent.Create(new { VideoId = videoId })
        };
        request.Headers.Add("X-Test-User-Id", user.Id.ToString());
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("X-Device-Id", deviceId.ToString());
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Signature", signature);

        var response = await Client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"Response body was: {body}");
    }
}
