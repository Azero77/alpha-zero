using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests.Integration;

public class DeviceLockingTests : BaseIntegrationTest
{
    private readonly RSA _rsa;
    private readonly string _publicKeyPem;

    public DeviceLockingTests(ApiFactory factory) : base(factory)
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
    public async Task Authorize_ShouldSucceed_WhenRequestFromMainDeviceWithValidSignature()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        // 1. Setup User and Main Device
        var user = TenantUser.Create(tenantId, "main-user-sub", "Main User").Value;
        var deviceId = Guid.NewGuid();
        user.RegisterDevice("My iPhone", DevicePlatform.Ios, _publicKeyPem, DateTime.UtcNow);
        
        // Use reflection to set the internal device ID to match our test requirement or just get the one created
        var registeredDevice = user.Devices.First();
        user.SetMainDevice(registeredDevice.Id, DateTime.UtcNow);

        DbContext.TenantUsers.Add(user);
        await DbContext.SaveChangesAsync();

        // 2. Setup Policy with IsMainDevice condition
        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "LockedPolicy", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "courses:View" }, true, condition) 
        });

        var principal = Principal.Create(Guid.NewGuid(), "locked-role", "hash", "Locked Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);

        var principalRepo = Resolve<IPrincipalRepository>();
        var managedPolicyRepo = Resolve<IManagedPolicyRepository>();
        managedPolicyRepo.Add(managedPolicy);
        principalRepo.Add(principal);
        await DbContext.SaveChangesAsync();

        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:course:{tenantId}:course/locked-101").Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        var evaluator = Resolve<IPolicyEvaluatorService>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = "course/locked-101";
        var signature = GenerateSignature(path, timestamp);

        // We need to ensure the IDeviceProvider and IHttpContextAccessor are set up for the OperationEvaluator
        // In this integration test, we can use the actual registered services.
        // We'll mock the HttpContext for the evaluator
        var httpContextAccessor = Resolve<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContext.Request.Path = "/" + path;

        httpContext.Request.Headers["X-Device-Id"] = registeredDevice.Id.ToString();
        httpContext.Request.Headers["X-Timestamp"] = timestamp;
        httpContext.Request.Headers["X-Signature"] = signature;
        httpContextAccessor.HttpContext = httpContext;

        var authContext = new AuthorizationContext
        {
            Id = user.Id,
            TenantId = tenantId,
            ResourcePath = path,
            ResourceType = "course",
            RequiredPermission = "courses:View",
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
            DeviceId = registeredDevice.Id.ToString(),
            UserMainDeviceId = registeredDevice.Id.ToString()
        };

        // Act
        var result = await evaluator.Authorize(authContext);

        // Assert
        result.IsError.Should().BeFalse("Main device with valid signature should be allowed");
    }

    [Fact]
    public async Task Authorize_ShouldFail_WhenRequestFromSecondaryDevice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var user = TenantUser.Create(tenantId, "multi-device-sub", "Multi User").Value;
        
        // Register two devices
        user.RegisterDevice("Primary", DevicePlatform.Ios, _publicKeyPem, DateTime.UtcNow);
        var primaryId = user.Devices.First().Id;
        
        user.RegisterDevice("Secondary", DevicePlatform.Web, "other-pub-key", DateTime.UtcNow);
        var secondaryId = user.Devices.Last().Id;

        user.SetMainDevice(primaryId, DateTime.UtcNow);
        DbContext.TenantUsers.Add(user);

        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "LockedPolicy2", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "courses:View" }, true, condition) 
        });

        var principal = Principal.Create(Guid.NewGuid(), "locked-role-2", "hash", "Locked Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);
        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);
        
        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:course:{tenantId}:course/locked-202").Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        var evaluator = Resolve<IPolicyEvaluatorService>();

        var authContext = new AuthorizationContext
        {
            Id = user.Id,
            TenantId = tenantId,
            ResourcePath = "course/locked-202",
            ResourceType = "course",
            RequiredPermission = "courses:View",
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
            DeviceId = secondaryId.ToString(),
            UserMainDeviceId = primaryId.ToString()
        };

        // Act
        var result = await evaluator.Authorize(authContext);

        // Assert
        result.IsError.Should().BeTrue("Secondary device should be rejected for locked resource");
        result.FirstError.Code.Should().Be("Condition.IsMainDeviceFailed");
    }

    [Fact]
    public async Task Authorize_ShouldFail_WhenPrincipalIsNotAUser()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        // Policy requiring main device
        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "NonUserLockedPolicy", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "identity:Admin" }, true, condition) 
        });

        // A non-user principal (e.g., a system role)
        var principal = Principal.Create(Guid.NewGuid(), "system-admin", "hash", "System Admin", PrincipalType.User, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);

        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);
        await DbContext.SaveChangesAsync();

        var evaluator = Resolve<IPolicyEvaluatorService>();

        // Note: Non-user principals have UserMainDeviceId = null in context
        var authContext = new AuthorizationContext
        {
            Id = principal.Id,
            TenantId = tenantId,
            ResourcePath = "admin/tools",
            ResourceType = "principal",
            RequiredPermission = "identity:Admin",
            AuthenticationMethod = AuthenticationMethod.Principal.ToString(),
            DeviceId = Guid.NewGuid().ToString(),
            UserMainDeviceId = null // Explicitly null for non-users
        };

        // Act
        var result = await evaluator.Authorize(authContext);

        // Assert
        result.IsError.Should().BeTrue("Non-user principals cannot satisfy IsMainDevice condition");
    }

    [Fact]
    public async Task Authorize_ShouldFail_WhenSignatureIsInvalid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var user = TenantUser.Create(tenantId, "sig-fail-sub", "Sig User").Value;
        user.RegisterDevice("My iPhone", DevicePlatform.Ios, _publicKeyPem, DateTime.UtcNow);
        var registeredDevice = user.Devices.First();
        user.SetMainDevice(registeredDevice.Id, DateTime.UtcNow);

        DbContext.TenantUsers.Add(user);

        var condition = new ConditionNode("DeviceId", Operator.IsMainDevice, JsonDocument.Parse("true").RootElement);
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "LockedPolicy3", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "courses:View" }, true, condition) 
        });

        var principal = Principal.Create(Guid.NewGuid(), "locked-role-3", "hash", "Locked Role", PrincipalType.Role, null, tenantId).Value;
        principal.AddPolicy(managedPolicy);
        Resolve<IManagedPolicyRepository>().Add(managedPolicy);
        Resolve<IPrincipalRepository>().Add(principal);
        
        var assignment = TenantUserPrincipalAssignment.Create(tenantId, user, principal, $"az:course:{tenantId}:course/locked-303").Value;
        DbContext.TenantPrincipalAssignments.Add(assignment);
        await DbContext.SaveChangesAsync();

        var evaluator = Resolve<IPolicyEvaluatorService>();
        
        // Setup HttpContext with INVALID signature
        var httpContextAccessor = Resolve<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var path = "course/locked-303";
        httpContext.Request.Path = "/" + path;
        httpContext.Request.Headers["X-Device-Id"] = registeredDevice.Id.ToString();
        httpContext.Request.Headers["X-Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        httpContext.Request.Headers["X-Signature"] = "invalid-base64-signature";
        httpContextAccessor.HttpContext = httpContext;

        var authContext = new AuthorizationContext
        {
            Id = user.Id,
            TenantId = tenantId,
            ResourcePath = "course/locked-303",
            ResourceType = "course",
            RequiredPermission = "courses:View",
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
            DeviceId = registeredDevice.Id.ToString(),
            UserMainDeviceId = registeredDevice.Id.ToString()
        };

        // Act
        var result = await evaluator.Authorize(authContext);

        // Assert
        result.IsError.Should().BeTrue("Invalid signature should trigger forbidden");
    }
}
