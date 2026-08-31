using System.Security.Claims;
using AlphaZero.Modules.Identity.Application.Auth.Extensions;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using NSubstitute;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Auth;

public class ClaimsMappingTests
{
    private readonly IClock _clock;

    public ClaimsMappingTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void TenantUser_ToClaims_Should_MapAllTenantUserProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "cognito-sub-123", "Test Student").Value;
        user.RegisterDevice("Pixel 8", DevicePlatform.Android, "device-pub-key-rsa", _clock.Now);
        var device = user.Devices.First();
        user.SetMainDevice(device.Id, _clock.Now, skipCooldown: true);

        // Act
        var claims = user.ToClaims(device, _clock);

        // Assert
        claims.Should().Contain(c => c.Key == CustomClaimTypes.IdentityId && c.Value == "cognito-sub-123");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.Name && c.Value == "Test Student");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.DeviceId && c.Value == device.Id.ToString());
        claims.Should().Contain(c => c.Key == CustomClaimTypes.DeviceName && c.Value == "Pixel 8");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.DevicePlatform && c.Value == "Android");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.DevicePublicKey && c.Value == "device-pub-key-rsa");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.MainDeviceId && c.Value == device.Id.ToString());
        claims.Should().Contain(c => c.Key == CustomClaimTypes.Iat);
    }

    [Fact]
    public void Principal_ToClaims_Should_MapAllPrincipalProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var principal = Principal.Create(
            Guid.NewGuid(),
            "teacher_john",
            "hashed_pwd",
            "John Doe",
            PrincipalType.User,
            "az:course:*:*",
            tenantId).Value;

        // Act
        var claims = principal.ToClaims(_clock);

        // Assert
        claims.Should().Contain(c => c.Key == CustomClaimTypes.Name && c.Value == "John Doe");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.Username && c.Value == "teacher_john");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.PrincipalType && c.Value == "User");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.IsManaged && c.Value == "false");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.IsGlobal && c.Value == "false");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.PrincipalScope && c.Value == "az:course:*:*");
        claims.Should().Contain(c => c.Key == CustomClaimTypes.Iat);
    }

    [Fact]
    public void ClaimsPrincipal_ToTenantUserDTO_Should_CorrectlyExtractDTO()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("tid", tenantId.ToString()),
            new("identity_id", "cognito-identity-xyz"),
            new("name", "Jane Doe"),
            new("device_id", deviceId.ToString()),
            new("device_name", "iPhone 15"),
            new("device_platform", "Ios"),
            new("device_public_key", "pk-1234"),
            new("main_device_id", deviceId.ToString())
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var dto = claimsPrincipal.ToTenantUserDTO();

        // Assert
        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(userId);
        dto.TenantId.Should().Be(tenantId);
        dto.IdentityId.Should().Be("cognito-identity-xyz");
        dto.Name.Should().Be("Jane Doe");
        dto.DeviceId.Should().Be(deviceId);
        dto.DeviceName.Should().Be("iPhone 15");
        dto.DevicePlatform.Should().Be("Ios");
        dto.PublicKey.Should().Be("pk-1234");
        dto.MainDeviceId.Should().Be(deviceId);
    }

    [Fact]
    public void ClaimsPrincipal_ToPrincipalDTO_Should_CorrectlyExtractDTO()
    {
        // Arrange
        var principalId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new("sub", principalId.ToString()),
            new("tid", tenantId.ToString()),
            new("username", "director_mark"),
            new("name", "Mark Director"),
            new("principal_type", "User"),
            new("is_managed", "false"),
            new("is_global", "false"),
            new("principal_scope", "az:*:*:*")
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var dto = claimsPrincipal.ToPrincipalDTO();

        // Assert
        dto.Should().NotBeNull();
        dto!.PrincipalId.Should().Be(principalId);
        dto.TenantId.Should().Be(tenantId);
        dto.Username.Should().Be("director_mark");
        dto.Name.Should().Be("Mark Director");
        dto.PrincipalType.Should().Be("User");
        dto.IsManaged.Should().BeFalse();
        dto.IsGlobal.Should().BeFalse();
        dto.PrincipalScope.Should().Be("az:*:*:*");
    }

    [Fact]
    public void DTO_ToClaims_Should_RoundTripWithClaimsPrincipal()
    {
        // Arrange
        var originalDto = new TenantUserDTO(
            UserId: Guid.NewGuid(),
            IdentityId: "cognito-abc",
            Name: "Test Name",
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            DeviceName: "Chrome Web",
            DevicePlatform: "Web",
            PublicKey: "pub-key",
            MainDeviceId: Guid.NewGuid());

        // Act: DTO -> Claims -> ClaimsPrincipal -> DTO
        var claimDtos = originalDto.ToClaims(_clock);
        var claims = claimDtos.Select(c => new Claim(c.Key, c.Value)).ToList();
        // Add sub
        claims.Add(new Claim("sub", originalDto.UserId.ToString()));
        var cp = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var roundTrippedDto = cp.ToTenantUserDTO();

        // Assert
        roundTrippedDto.Should().NotBeNull();
        roundTrippedDto.Should().BeEquivalentTo(originalDto);
    }
}
