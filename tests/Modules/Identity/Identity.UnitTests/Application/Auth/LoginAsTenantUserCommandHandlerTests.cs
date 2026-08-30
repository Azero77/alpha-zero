using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Auth;

public class LoginAsTenantUserCommandHandlerTests
{
    private readonly IRepository<TenantUser> _userRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<LoginAsTenantUserCommandHandler> _logger;
    private readonly IClock _clock;
    private readonly LoginAsTenantUserCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string IdentityId = "cognito-sub";

    public LoginAsTenantUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        _jwtProvider = Substitute.For<IJwtProvider>();
        _logger = Substitute.For<ILogger<LoginAsTenantUserCommandHandler>>();
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTime.UtcNow);
        _handler = new LoginAsTenantUserCommandHandler(_userRepository, _logger, _jwtProvider, _clock);
    }

    [Fact]
    public async Task Handle_Should_CreateUser_AndRegisterMainDevice_WhenUserDoesNotExist()
    {
        // Arrange
        // User does not exist
        _userRepository.GetFirst(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _jwtProvider.GenerateToken(Arg.Any<Guid>(), TenantId, AuthenticationMethod.TenantUser, Arg.Any<List<ClaimDTO>>())
            .Returns("token-123");

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali", "pub-key", "Test Device", DevicePlatform.Web);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("token-123");
        
        // Assert that the user was added
        _userRepository.Received(1).Add(Arg.Is<TenantUser>(u => 
            u.IdentityId == IdentityId && 
            u.TenantId == TenantId && 
            u.Name == "Ali" &&
            u.Devices.Count == 1 &&
            u.Devices.First().PublicKey == "pub-key" &&
            u.MainDeviceId == u.Devices.First().Id
        ));

        _jwtProvider.Received(1).GenerateToken(
            Arg.Any<Guid>(), 
            TenantId, 
            AuthenticationMethod.TenantUser, 
            Arg.Is<List<ClaimDTO>>(claims => 
                claims.Any(c => c.Key == CustomClaimTypes.IdentityId && c.Value == IdentityId) &&
                claims.Any(c => c.Key == CustomClaimTypes.Name && c.Value == "Ali") &&
                claims.Any(c => c.Key == CustomClaimTypes.DeviceName && c.Value == "Test Device") &&
                claims.Any(c => c.Key == CustomClaimTypes.DevicePublicKey && c.Value == "pub-key") &&
                claims.Any(c => c.Key == CustomClaimTypes.DevicePlatform && c.Value == DevicePlatform.Web.ToString())
            ));
    }

    [Fact]
    public async Task Handle_Should_RegisterDevice_WhenUserExistsButDeviceIsNew()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, IdentityId, "Ali").Value;
        // User already has an old device
        user.RegisterDevice("Old Device", DevicePlatform.Ios, "old-key", _clock.Now);
        var oldDeviceId = user.Devices.First().Id;
        user.SetMainDevice(oldDeviceId, _clock.Now, skipCooldown: true);

        _userRepository.GetFirst(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _jwtProvider.GenerateToken(user.Id, TenantId, AuthenticationMethod.TenantUser, Arg.Any<List<ClaimDTO>>())
            .Returns("token-123");

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali", "new-key", "New Device", DevicePlatform.Android);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("token-123");
        
        // The user was updated
        _userRepository.Received(1).Update(Arg.Is<TenantUser>(u => 
            u.Devices.Count == 2 &&
            u.Devices.Any(d => d.PublicKey == "new-key") &&
            u.MainDeviceId == oldDeviceId // Main device should not change since it's not the first device
        ));
    }

    [Fact]
    public async Task Handle_Should_NotRegisterDevice_WhenDeviceAlreadyExists()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, IdentityId, "Ali").Value;
        user.RegisterDevice("Existing Device", DevicePlatform.Web, "existing-key", _clock.Now);
        var existingDeviceId = user.Devices.First().Id;

        _userRepository.GetFirst(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _jwtProvider.GenerateToken(user.Id, TenantId, AuthenticationMethod.TenantUser, Arg.Any<List<ClaimDTO>>())
            .Returns("token-123");

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali", "existing-key", "Existing Device", DevicePlatform.Web);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("token-123");
        result.Value.DeviceId.Should().Be(existingDeviceId);
        
        // Ensure no new device was added
        user.Devices.Count.Should().Be(1);
        _userRepository.Received(1).Update(user);
    }
}
