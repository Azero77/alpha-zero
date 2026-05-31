using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
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
    private readonly LoginAsTenantUserCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string IdentityId = "cognito-sub";

    public LoginAsTenantUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        _jwtProvider = Substitute.For<IJwtProvider>();
        _logger = Substitute.For<ILogger<LoginAsTenantUserCommandHandler>>();
        _handler = new LoginAsTenantUserCommandHandler(_userRepository, _logger, _jwtProvider);
    }

    [Fact]
    public async Task Handle_Should_ReturnToken_WhenUserIsEnrolled()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, IdentityId, "Ali", TenantUserDeviceInfo.Empty).Value;

        _userRepository.GetFirst(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _jwtProvider.GenerateToken(user.Id, TenantId, AuthenticationMethod.TenantUser)
            .Returns("token-123");

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("token-123");
        result.Value.TenantUserId.Should().Be(user.Id);
        
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenUserIsNotEnrolled()
    {
        // Arrange
        _userRepository.GetFirst(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotEnrolled");
    }
}
